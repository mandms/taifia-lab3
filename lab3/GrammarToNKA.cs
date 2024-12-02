using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

class GrammarToNKA
{
    private static string leftRegrex = @"^\s*<(\w+)>\s*->\s*((?:<\w+>\s+)?[\wε](?:\s*\|\s*(?:<\w+>\s+)?[\wε])*)\s*$";
    private static string rightRegex = @"^\s*<(\w+)>\s*->\s*([\wε](?:\s+<\w+>)?(?:\s*\|\s*[\wε](?:\s+<\w+>)?)*)\s*$";
    private static string findNonTerminal = @"<(.*?)>";
    private static string findTerminal = @"\b(?!<)(\w+)(?!>)\b";

    private static void WriteToFile(string outFile, List<List<string>> result)
    {
        using (StreamWriter writer = new StreamWriter(outFile))
        {
            foreach (var row in result)
            {
                writer.WriteLine(string.Join(";", row));
            }
        }
    }

    private static string GetType(string inFile)
    {
        foreach (var line in File.ReadLines(inFile))
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;
            if (Regex.IsMatch(trimmedLine, leftRegrex)) return "left";
            else if (Regex.IsMatch(trimmedLine, rightRegex)) return "right";
        }
        return null;
    }

    private static Dictionary<string, List<string>> GetRules(string inFile)
    {
        var rules = new Dictionary<string, List<string>>();
        string lastRule = null;

        foreach (var line in File.ReadLines(inFile))
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            var parts = trimmedLine.Split(new string[] { "->" }, StringSplitOptions.None);
            string rightSide;
            List<string> productions;
            if (parts.Length != 2 && lastRule != null)
            {
                rightSide = parts[0].Trim();
                productions = rightSide.Split('|').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                rules[lastRule].AddRange(productions);
                continue;
            }

            string leftSide = parts[0].Trim();
            lastRule = leftSide;
            rightSide = parts[1].Trim();

            productions = rightSide.Split('|').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

            if (!productions.Any()) continue;

            if (rules.ContainsKey(leftSide))
                rules[leftSide].AddRange(productions);
            else
                rules[leftSide] = productions;
        }

        return rules;
    }

    private static List<string> GetTerminals(Dictionary<string, List<string>> rules)
    {
        var terminals = new HashSet<string>();

        foreach (var productions in rules.Values)
        {
            foreach (var val in productions)
            {
                var items = val.Split(' ');
                foreach (var item in items)
                {
                    if (!item.StartsWith("<") && !item.EndsWith(">"))
                    {
                        terminals.Add(item);
                    }
                }
            }
        }

        return terminals.ToList();
    }

    private static Dictionary<string, string> FillStateMapping(Dictionary<string, List<string>> rules, string type)
    {
        var rulesStatesMap = new Dictionary<string, string>();
        int stateCounter = 1;

        if (type == "left")
        {
            rulesStatesMap["H"] = "q0";

            foreach (var left in rules.Keys.Reverse())
            {
                string leftState = $"q{stateCounter}";
                rulesStatesMap[left] = leftState;
                stateCounter++;
            }
        }
        else if (type == "right")
        {
            stateCounter = 0;
            foreach (var left in rules.Keys)
            {
                string leftState = $"q{stateCounter}";
                rulesStatesMap[left] = leftState;
                stateCounter++;
            }
            rulesStatesMap["F"] = $"q{rules.Count}";
        }

        return rulesStatesMap;
    }

    private static List<List<string>> ToStates(Dictionary<string, List<string>> rules, Dictionary<string, string> statesMap, string type)
    {
        var terminals = GetTerminals(rules).OrderBy(t => t).ToList();
        var result = new List<List<string>>();

        for (int i = 0; i < terminals.Count + 2; i++)
        {
            result.Add(new List<string>(new string[rules.Count + 2]));
        }

        for (int i = 2; i < result.Count; i++)
        {
            result[i][0] = terminals[i - 2];
        }

        result[0][result[0].Count - 1] = "F";

        int stateIndex = 1;
        foreach (var state in statesMap.Values)
        {
            result[1][stateIndex] = state;
            stateIndex++;
        }

        foreach (var rule in rules)
        {
            string currState = statesMap[rule.Key];
            foreach (var production in rule.Value)
            {
                string[] parts = production.Split(' ');

                if (type == "left")
                {
                    if (parts[0].StartsWith("<") && parts[0].EndsWith(">"))
                    {
                        var nonTerminalMatch = Regex.Match(parts[0], findNonTerminal);
                        var terminalMatch = Regex.Match(parts[1], findTerminal);
                        if (nonTerminalMatch.Success && terminalMatch.Success)
                        {
                            int ruleIdx = result[1].IndexOf(statesMap[$"<{nonTerminalMatch.Groups[1].Value}>"]);
                            int columnIdx = terminals.IndexOf(terminalMatch.Groups[1].Value);
                            if (string.IsNullOrEmpty(result[columnIdx + 2][ruleIdx]))
                                result[columnIdx + 2][ruleIdx] = currState;
                            else
                                result[columnIdx + 2][ruleIdx] += $",{currState}";
                        }
                    }
                    else
                    {
                        int lineIdx = terminals.IndexOf(parts[0]);
                        if (string.IsNullOrEmpty(result[lineIdx + 2][1]))
                            result[lineIdx + 2][1] = currState;
                        else
                            result[lineIdx + 2][1] += $",{currState}";
                    }
                }
                else if (type == "right")
                {
                    int ruleIdx = result[1].IndexOf(currState);
                    int lineIdx = terminals.IndexOf(Regex.Match(parts[0], findTerminal).Groups[1].Value);
                    if ((parts.Length > 1) && parts[1].StartsWith("<") && parts[1].EndsWith(">"))
                    {
                        var nonTerminalMatch = Regex.Match(parts[1], findNonTerminal);
                        if (nonTerminalMatch.Success)
                        {
                            string nonTerminalState = statesMap[$"<{nonTerminalMatch.Groups[1].Value}>"];
                            if (string.IsNullOrEmpty(result[lineIdx + 2][ruleIdx]))
                                result[lineIdx + 2][ruleIdx] = nonTerminalState;
                            else
                                result[lineIdx + 2][ruleIdx] += $",{nonTerminalState}";
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(result[lineIdx + 2][ruleIdx]))
                            result[lineIdx + 2][ruleIdx] = statesMap["F"];
                        else
                            result[lineIdx + 2][ruleIdx] += $",{statesMap["F"]}";
                    }
                }
            }
        }

        return result;
    }

    public static void ToNKA(string inFile, string outFile)
    {
        string type = GetType(inFile);
        if (type == null) return;

        var rules = GetRules(inFile);
        var statesMap = FillStateMapping(rules, type);
        var states = ToStates(rules, statesMap, type);
        WriteToFile(outFile, states);
    }
}
