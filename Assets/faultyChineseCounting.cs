using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using rnd = UnityEngine.Random;

public class faultyChineseCounting : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] keys;
    public KMSelectable[] ledButtons;
    public TextMesh[] labels;
    public Renderer[] leds;
    public Transform moduleTransform;
    public Color[] colors;
    public Color[] textColors;

    private int stage;
    private int[] solution = new int[4];
    private int[] keyColors = new int[4];
    private int[] ledColors = new int[2];
    private int[] displays = new int[4];
    private int[] values = new int[4];
    private int specialCase;
    private int specialCaseUses;

    private static readonly string[] positionNames = new string[] { "top left", "top right", "bottom left", "bottom right" };
    private static readonly string[] ledColorNames = new string[] { "white", "red", "green", "orange" };
    private static readonly string[] textColorNames = new string[] { "black", "red", "blue", "green", "purple" };

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;
    private bool cantPress;
    private bool cycling;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable key in keys)
            key.OnInteract += delegate () { PressKey(key, true); return false; };
        foreach (KMSelectable led in ledButtons)
            led.OnInteract += delegate () { PressLed(led); return false; };
    }

    private void Start()
    {
        specialCase = rnd.Range(0, 5);
        Debug.LogFormat("[Faulty Chinese Counting #{0}] Special case:", moduleId);
        switch (specialCase)
        {
            case 0:
                specialCaseUses = rnd.Range(0, 4);
                Debug.LogFormat("[Faulty Chinese Counting #{0}] The {1} key is missing a label.", moduleId, positionNames[specialCaseUses]);
                labels[specialCaseUses].gameObject.SetActive(false);
                break;
            case 1:
                specialCaseUses = rnd.Range(0, 4);
                Debug.LogFormat("[Faulty Chinese Counting #{0}] The {1} key is cycling 3 different labels.", moduleId, positionNames[specialCaseUses]);
                break;
            case 2:
                specialCaseUses = rnd.Range(0, 2);
                Debug.LogFormat("[Faulty Chinese Counting #{0}] The {1} LED is missing.", moduleId, specialCaseUses == 0 ? "left" : "right");
                leds[specialCaseUses].gameObject.SetActive(false);
                break;
            case 3:
                specialCaseUses = rnd.Range(0, 4);
                Debug.LogFormat("[Faulty Chinese Counting #{0}] The {1} key disappears when the module is selected.", moduleId, positionNames[specialCaseUses]);
                GetComponent<KMSelectable>().OnFocus += delegate () { keys[specialCaseUses].gameObject.SetActive(false); };
                GetComponent<KMSelectable>().OnDefocus += delegate () { keys[specialCaseUses].gameObject.SetActive(true); };
                break;
            case 4:
                Debug.LogFormat("[Faulty Chinese Counting #{0}] The module is upside down.", moduleId);
                moduleTransform.Rotate(0, 180, 0);
                break;
            default:
                throw new FormatException("specialCase has an unexpected value (expeted 0-4).");
        }
        GenerateModule();
    }

    private void GenerateModule()
    {
        for (int i = 0; i < 4; i++)
        {
            keyColors[i] = rnd.Range(0, 5);
            displays[i] = rnd.Range(1, 101);
        }
        for (int i = 0; i < 2; i++)
            ledColors[i] = rnd.Range(0, 4);
        while (specialCase == 1 && ledColors[0] == ledColors[1])
            ledColors[1] = rnd.Range(0, 4);
        switch (specialCase)
        {
            case 0:
                displays[specialCaseUses] = (bomb.GetSerialNumberNumbers().Last() * bomb.GetBatteryCount()) % 100;
                keyColors[specialCaseUses] = keyColors.Where((_, i) => i != specialCaseUses).Sum() / 3;
                break;
            case 2:
                var number = Convert.ToString(bomb.GetBatteryHolderCount() * bomb.GetPortCount() % 15, 2).PadLeft(4, 'X');
                ledColors[specialCaseUses] = number.IndexOf(specialCaseUses == 0 ? '0' : '1');
                if (!Enumerable.Range(0, 4).Contains(ledColors[specialCaseUses]))
                    ledColors[specialCaseUses] = 0;
                break;
            default:
                break;
        }
        StartCoroutine(LedCycle());
        for (int i = 0; i < 4; i++)
        {
            values[i] = GetKeyValue(i);
            labels[i].text = ChineseNumber(displays[i]);
            labels[i].color = textColors[specialCase == 4 ? keyColors[(i + 2) % 4] : keyColors[i]];
        }
        Debug.LogFormat("[Faulty Chinese Counting #{0}] LED colors: {1}", moduleId, ledColors.Select(x => ledColorNames[x]).Join(", "));
        Debug.LogFormat("[Faulty Chinese Counting #{0}] Text colors: {1}", moduleId, keyColors.Select(x => textColorNames[x]).Join(", "));
        Debug.LogFormat("[Faulty Chinese Counting #{0}] Displayed numbers: {1} ({2})", moduleId, displays.Select(x => ChineseNumber(x)).Join(", "), displays.Join(", "));
        Debug.LogFormat("[Faulty Chinese Counting #{0}] Actual values: {1}", moduleId, values.Join(", "));
        var table = "ACHD,HDAC,CHDA,HACD".Split(',');
        switch (table[ledColors[0]][ledColors[1]])
        {
            case 'A':
                solution = values.Select((x, i) => new { value = x, index = i }).OrderBy(x => x.value).Select(x => x.index).ToArray();
                Debug.LogFormat("[Chinese Counting #{0}] The numbers should be pressed in asending order, by value.", moduleId);
                break;
            case 'D':
                solution = values.Select((x, i) => new { value = x, index = i }).OrderByDescending(x => x.value).Select(x => x.index).ToArray();
                Debug.LogFormat("[Chinese Counting #{0}] The numbers should be pressed in descending order, by value.", moduleId);
                break;
            case 'C':
                solution = values.Select((x, i) => new { value = x, index = i }).OrderBy(x => ChineseNumber(x.value).Length).ThenByDescending(x => x.value).Select(x => x.index).ToArray();
                Debug.LogFormat("[Chinese Counting #{0}] The numbers should be pressed in ascending order, by number of characters.", moduleId);
                break;
            default:
                solution = values.Select((x, i) => new { value = x, index = i }).OrderByDescending(x => ChineseNumber(x.value).Length).ThenBy(x => x.value).Select(x => x.index).ToArray();
                Debug.LogFormat("[Chinese Counting #{0}] The numbers should be pressed in descending order, by number of characters.", moduleId);
                break;
        }
        Debug.LogFormat("[Faulty Chinese Counting #{0}] Solution: {1}", moduleId, solution.Select(x => positionNames[x]).Join(", "));
    }

    private int GetKeyValue(int i)
    {
        switch (keyColors[i])
        {
            case 0:
                return displays[i];
            case 1:
                return 100 - displays[i];
            case 2:
                return displays[i] * 2;
            case 3:
                return displays[i] + displays[3 - i];
            case 4:
                return displays.Sum() / 4;
        }
        return 0;
    }

    private string ChineseNumber(int i)
    {
        var digits = "一二三四五六七八九";
        if (i == 100)
            return "一百";
        else if (i == 200)
            return "二百";
        else if (i > 100 && i < 200)
            return "一百" + ChineseNumber(i - 100);
        else if (i == 0)
            return "〇";
        else if (i.ToString().Length == 1)
            return digits[i - 1].ToString();
        else if (i == 10)
            return "十";
        else if (i < 20)
            return "十" + digits[(i % 10) - 1];
        else if (i % 10 == 0)
            return digits[(i / 10) - 1] + "十";
        else
        {
            var x = digits[int.Parse(i.ToString()[0].ToString()) - 1];
            var y = digits[int.Parse(i.ToString()[1].ToString()) - 1];
            return x + "十" + y;
        }
    }

    private IEnumerator LedCycle()
    {
        cycling = true;
        cantPress = false;
        var cycle1 = Enumerable.Range(0, 4).Where(x => x != ledColors[0]).ToList().Shuffle().ToArray();
        var cycle2 = Enumerable.Range(0, 4).Where(x => x != ledColors[1]).ToList().Shuffle().ToArray();
        var cycle3 = new int[4];
        var cycle4 = new int[4];
        for (int i = 0; i < 3; i++)
        {
            cycle3[i] = i == Array.IndexOf(cycle2, ledColors[0]) ? displays[specialCaseUses] : rnd.Range(1, 101);
            cycle4[i] = i == Array.IndexOf(cycle2, ledColors[0]) ? keyColors[specialCaseUses] : rnd.Range(0, 5);
        }
        while (cycling)
        {
            for (int i = 0; i < 3; i++)
            {
                leds[0].material.color = colors[cycle1[i]];
                leds[1].material.color = colors[cycle2[i]];
                if (specialCase == 1)
                {
                    labels[specialCaseUses].text = ChineseNumber(cycle3[i]);
                    labels[specialCaseUses].color = textColors[cycle4[i]];
                }
                yield return new WaitForSeconds(1f);
            }
        }
    }

    private void PressKey(KMSelectable key, bool fromInteraction)
    {
        if (fromInteraction)
        {
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, key.transform);
            key.AddInteractionPunch(.5f);
        }
        if (moduleSolved || cantPress)
            return;
        var ix = Array.IndexOf(keys, key);
        Debug.LogFormat("[Faulty Chinese Counting #{0}] You pressed the {1} key.", moduleId, positionNames[ix]);
        if (ix != solution[stage])
        {
            Debug.LogFormat("[Faulty Chinese Counting #{0}] That was incorrect. Strike!", moduleId);
            Debug.LogFormat("[Faulty Chinese Counting #{0}] Resetting...", moduleId);
            module.HandleStrike();
            stage = 0;
            cycling = false;
            StopAllCoroutines();
            StartCoroutine(Strike());
        }
        else
        {
            Debug.LogFormat("[Faulty Chinese Counting #{0}] That was correct.", moduleId);
            stage++;
        }
        if (stage == 4)
        {
            StopAllCoroutines();
            module.HandlePass();
            moduleSolved = true;
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
            foreach (Renderer led in leds)
                led.material.color = Color.black;
            Debug.LogFormat("[Faulty Chinese Counting #{0}] Module solved!", moduleId);
        }
    }

    private void PressLed(KMSelectable led)
    {
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, led.transform);
        led.AddInteractionPunch(.5f);
        if (moduleSolved || cantPress || specialCase != 3)
            return;
        var ix = Array.IndexOf(ledButtons, led);
        if (ix != specialCaseUses % 2)
            PressKey(keys[specialCaseUses], false);
    }

    private IEnumerator Strike()
    {
        foreach (Renderer led in leds)
            led.material.color = Color.black;
        cantPress = true;
        yield return new WaitForSeconds(0.5f);
        GenerateModule();
    }

    // Twitch Plays
    private bool cmdIsValid(string param)
    {
        string[] parameters = param.Split(' ', ',');
        for (int i = 1; i < parameters.Length; i++)
        {
            if (!parameters[i].EqualsIgnoreCase("1") && !parameters[i].EqualsIgnoreCase("2") && !parameters[i].EqualsIgnoreCase("3") && !parameters[i].EqualsIgnoreCase("4") && !parameters[i].EqualsIgnoreCase("tl") && !parameters[i].EqualsIgnoreCase("tr") && !parameters[i].EqualsIgnoreCase("bl") && !parameters[i].EqualsIgnoreCase("br") && !parameters[i].EqualsIgnoreCase("topleft") && !parameters[i].EqualsIgnoreCase("topright") && !parameters[i].EqualsIgnoreCase("bottomleft") && !parameters[i].EqualsIgnoreCase("bottomright") && !parameters[i].EqualsIgnoreCase("l") && !parameters[i].EqualsIgnoreCase("r") && !parameters[i].EqualsIgnoreCase("left") && !parameters[i].EqualsIgnoreCase("right"))
            {
                return false;
            }
        }
        return true;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <button> [Presses the specified button] | !{0} press <button> <button> [Example of button chaining] | !{0} reset [Resets all inputs] | Valid buttons are tl, tr, bl, br OR 1-4 being the buttons from in reading order. Additionally, l, r, left, and right can be used to press the LEDs.";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*reset\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            Debug.LogFormat("[Faulty Chinese Counting #{0}] Reset of inputs triggered! (TP)", moduleId);
            stage = 0;
            yield break;
        }
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length > 1)
            {
                if (cmdIsValid(command))
                {
                    yield return null;
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        if (parameters[i].EqualsIgnoreCase("1"))
                            keys[0].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("2"))
                            keys[1].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("3"))
                            keys[2].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("4"))
                            keys[3].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("tl") || parameters[i].EqualsIgnoreCase("topleft"))
                            keys[0].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("tr") || parameters[i].EqualsIgnoreCase("topright"))
                            keys[1].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("bl") || parameters[i].EqualsIgnoreCase("bottomleft"))
                            keys[2].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("br") || parameters[i].EqualsIgnoreCase("bottomright"))
                            keys[3].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("l") || parameters[i].EqualsIgnoreCase("left"))
                            ledButtons[0].OnInteract();
                        else if (parameters[i].EqualsIgnoreCase("r") || parameters[i].EqualsIgnoreCase("right"))
                            ledButtons[1].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }
            yield break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!moduleSolved)
        {
            if (specialCase == 3 && solution[stage] == specialCaseUses)
                ledButtons.First(x => Array.IndexOf(ledButtons, x) != specialCaseUses % 2).OnInteract();
            else
                keys[solution[stage]].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
    }
}
