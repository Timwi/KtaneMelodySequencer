﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class MelodySequencerScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMRuleSeedable RuleSeedable;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    private bool solveAnimationDone = false;

    public KMSelectable[] keys;
    public KMSelectable listen;
    public KMSelectable record;
    public KMSelectable move;
    public KMSelectable[] CycleBtns;
    public KMSelectable[] InstBtns;
    public AudioClip[] Sounds;
    public GameObject Part;
    public GameObject ListenNotes;
    public Material[] KeysUnlit;
    public Material[] KeysLit;

    private int currentPart = 0;
    private int selectedPart = 0;
    private int keysPressed = 0;
    private int partsCreated = 0;
    private int currentInst;

    private bool listenActive = false;
    private bool moveActive = false;
    private bool recordActive = false;
    private Coroutine displayCoroutine = null;
    private Coroutine[] keyFlashCoroutines = null;

    private static readonly int[][] seed1parts = new[]
    {
        new[] { 2, 5, 9, 5, 10, 5, 9, 5 },
        new[] { 2, 5, 9, 12, 14, 9, 14, 12 },
        new[] { 17, 14, 17, 21, 22, 17, 22, 21 },
        new[] { 19, 16, 19, 16, 12, 16, 12, 9 },
        new[] { 7, 4, 7, 4, 9, 4, 9, 5 },
        new[] { 10, 5, 10, 7, 12, 7, 12, 9 },
        new[] { 14, 9, 14, 7, 12, 7, 12, 5 },
        new[] { 10, 5, 10, 4, 9, 4, 9, 0 },
    };

    private int[][] parts;
    private readonly int[][] moduleParts = new int[8][];
    private List<int> givenParts = new List<int>();

    private static readonly string[] noteNames = new[] { "C4", "C#4", "D4", "D#4", "E4", "F4", "F#4", "G4", "G#4", "A4", "A#4", "B4", "C5", "C#5", "D5", "D#5", "E5", "F5", "F#5", "G5", "G#5", "A5", "A#5", "B5", };

    private static readonly string[][] actualNoteNames = new[]
    {
        new[] {"mb_c4", "mb_cis4", "mb_d4", "mb_dis4", "mb_e4", "mb_f4", "mb_fis4", "mb_g4", "mb_gis4", "mb_a4", "mb_ais4", "mb_h4", "mb_c5", "mb_cis5", "mb_d5", "mb_dis5", "mb_e5", "mb_f5", "mb_fis5", "mb_g5", "mb_gis5", "mb_a5", "mb_ais5", "mb_h5"},
        new[] {"p_c4", "p_cis4", "p_d4", "p_dis4", "p_e4", "p_f4", "p_fis4", "p_g4", "p_gis4", "p_a4", "p_ais4", "p_h4", "p_c5", "p_cis5", "p_d5", "p_dis5", "p_e5", "p_f5", "p_fis5", "p_g5", "p_gis5", "p_a5", "p_ais5", "p_h5"},
        new[] {"xy_c5", "xy_cis5", "xy_d5", "xy_dis5", "xy_e5", "xy_f5", "xy_fis5", "xy_g5", "xy_gis5", "xy_a5", "xy_ais5", "xy_h5", "xy_c6", "xy_cis6", "xy_d6", "xy_dis6", "xy_e6", "xy_f6", "xy_fis6", "xy_g6", "xy_gis6", "xy_a6", "xy_ais6", "xy_h6"},
        new[] {"ha_c5", "ha_cis5", "ha_d5", "ha_dis5", "ha_e5", "ha_f5", "ha_fis5", "ha_g5", "ha_gis5", "ha_a5", "ha_ais5", "ha_h5", "ha_c6", "ha_cis6", "ha_d6", "ha_dis6", "ha_e6", "ha_f6", "ha_fis6", "ha_g6", "ha_gis6", "ha_a6", "ha_ais6", "ha_h6"}
    };
    void Awake()
    {
        moduleId = moduleIdCounter++;
        keyFlashCoroutines = new Coroutine[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].OnInteract += KeyPressed(i);
        }
        for (int i = 0; i < CycleBtns.Length; i++)
        {
            CycleBtns[i].OnInteract += CycBtnPressed(i);
        }
        for (int i = 0; i < InstBtns.Length; i++)
        {
            InstBtns[i].OnInteract += InstBtnPressed(i);
        }
        listen.OnInteract += delegate () { Listen(); return false; };
        record.OnInteract += delegate () { Record(); return false; };
        move.OnInteract += delegate () { Move(); return false; };

        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat(@"[Melody Sequencer #{0}] Using rule seed: {1}", moduleId, rnd.Seed);
        if (rnd.Seed == 1)
            parts = seed1parts;
        else
        {
            // Decide on the key of the first part. The rest of the parts are in specific keys relative to each previous one
            var keys = new List<int> { rnd.Next(0, 12) };
            for (int partIx = 1; partIx < 8; partIx++)
            {
                var eligibleKeys = new[] { 5, 7, -5, -7 }.Select(jump => keys[partIx - 1] + jump).Where(key => key >= 0 && key < 12).ToList();
                keys.Add(eligibleKeys[rnd.Next(0, eligibleKeys.Count)]);
            }

            // Generate a new melody at random!
            parts = new int[8][];
            for (int partIx = 0; partIx < 8; partIx++)
            {
                var notes = new[] { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19, 21, 23 }.Select(i => (i + keys[partIx]) % 24).ToArray();
                var majorNotes = new[] { 0, 4, 7, 12, 16, 19 }.Select(i => (i + keys[partIx]) % 24).ToArray();

                parts[partIx] = new int[8];

                // Make sure that we do not accidentally generate two identical parts
                do
                {
                    for (int note = 0; note < 8; note++)
                    {
                        var eligibleNotes = (note % 2 == 0 ? majorNotes : notes).ToList();
                        if (note > 0)
                            eligibleNotes.RemoveAll(n => Mathf.Abs(n - parts[partIx][note - 1]) >= 7);
                        else if (partIx > 0)
                            eligibleNotes.RemoveAll(n => Mathf.Abs(n - parts[partIx - 1].Last()) >= 7);
                        if (note > 1 && parts[partIx][note - 1] == parts[partIx][note - 2])
                            eligibleNotes.Remove(parts[partIx][note - 1]);
                        parts[partIx][note] = eligibleNotes[rnd.Next(0, eligibleNotes.Count)];
                    }
                }
                while (Enumerable.Range(0, partIx).Any(p => parts[p].SequenceEqual(parts[partIx])));

                Debug.LogFormat(@"[Melody Sequencer #{0}] Solution part {1}: {2}", moduleId, partIx + 1, string.Join(", ", parts[partIx].Select(note => noteNames[note]).ToArray()));
            }
        }
    }

    void Start()
    {
        var partNumbers = Enumerable.Range(0, parts.Length).ToList();
        var slotNumbers = Enumerable.Range(0, parts.Length).ToList();

        for (int i = 0; i < 4; i++)
        {
            int partIx = Random.Range(0, partNumbers.Count);
            int slotIx = Random.Range(0, slotNumbers.Count);
            moduleParts[slotNumbers[slotIx]] = parts[partNumbers[partIx]];
            givenParts.Add(partNumbers[partIx]);
            Debug.LogFormat(@"[Melody Sequencer #{0}] Slot {1} contains part {2}: {3}", moduleId, slotNumbers[slotIx] + 1, partNumbers[partIx] + 1, string.Join(", ", parts[partNumbers[partIx]].Select(note => noteNames[note]).ToArray()));
            partNumbers.RemoveAt(partIx);
            slotNumbers.RemoveAt(slotIx);
        }
        currentInst = 0;
        InstBtns[currentInst].GetComponentInChildren<TextMesh>().color = new Color32(255, 255, 255, 255);
        InstBtns[currentInst].GetComponent<MeshRenderer>().material = KeysUnlit[2];
    }

    private KMSelectable.OnInteractHandler KeyPressed(int keyPressed)
    {
        return delegate ()
        {
            if (listenActive || moveActive)
                return false;

            if (keyFlashCoroutines[keyPressed] != null)
                StopCoroutine(keyFlashCoroutines[keyPressed]);
            keyFlashCoroutines[keyPressed] = StartCoroutine(KeyFlash(keyPressed));

            if (recordActive)
                RecordInput(keyPressed);
            else
                displayCoroutine = StartCoroutine(DisplayFlash(keyPressed));

            return false;
        };
    }

    private KMSelectable.OnInteractHandler CycBtnPressed(int btnPressed)
    {
        return delegate ()
        {
            if (listenActive || moduleSolved)
                return false;

            if (btnPressed == 0)
                currentPart = (currentPart + 7) % 8;
            else
                currentPart = (currentPart + 1) % 8;

            Part.GetComponent<TextMesh>().text = (currentPart + 1).ToString();
            return false;
        };
    }

    private KMSelectable.OnInteractHandler InstBtnPressed(int btn)
    {
        return delegate
        {
            if (btn != currentInst)
            {
                InstBtns[currentInst].GetComponentInChildren<TextMesh>().color = new Color32(0, 0, 0, 255);
                InstBtns[currentInst].GetComponent<MeshRenderer>().material = KeysLit[2];
                InstBtns[btn].GetComponentInChildren<TextMesh>().color = new Color32(255, 255, 255, 255);
                InstBtns[btn].GetComponent<MeshRenderer>().material = KeysUnlit[2];
                currentInst = btn;
            }
            return false;
        };
    }

    void Listen()
    {
        if (listenActive || moveActive || recordActive || moduleSolved)
            return;
        if (moduleParts[currentPart] != null)
            StartCoroutine(Play());
    }

    void Record()
    {
        if (listenActive || moveActive || moduleSolved)
            return;

        if (givenParts.Contains(currentPart))
        {
            Debug.LogFormat(@"[Melody Sequencer #{0}] You tried to record part #{1} but that part is already given. Strike!", moduleId, currentPart + 1);
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Wrong";
            ListenNotes.SetActive(true);
            displayCoroutine = StartCoroutine(DisableText());
            GetComponent<KMBombModule>().HandleStrike();
            return;
        }

        if (moduleParts[currentPart] != null)
        {
            Debug.LogFormat(@"[Melody Sequencer #{0}] You tried to record in slot #{1} but that slot already has something in it. Strike!", moduleId, currentPart + 1);
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Wrong";
            ListenNotes.SetActive(true);
            displayCoroutine = StartCoroutine(DisableText());
            GetComponent<KMBombModule>().HandleStrike();
            return;
        }

        if (recordActive)
        {
            recordActive = false;
            keysPressed = 0;
            ListenNotes.GetComponent<TextMesh>().color = new Color32(230, 255, 0, 255);
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.1f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Canceled";
            ListenNotes.SetActive(true);
            displayCoroutine = StartCoroutine(DisableText());
            return;
        }

        ListenNotes.GetComponent<TextMesh>().text = "Record";
        ListenNotes.GetComponent<TextMesh>().color = new Color32(214, 31, 31, 255);
        ListenNotes.SetActive(true);
        recordActive = true;
    }

    void Move()
    {
        if (listenActive || recordActive || moduleSolved)
            return;

        if (!moveActive)
        {
            moveActive = true;
            selectedPart = currentPart;
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.07f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Selected Part: " + (selectedPart + 1);
            ListenNotes.SetActive(true);
        }
        else
        {
            if (parts[currentPart] == moduleParts[selectedPart])
            {
                int[] modulePartsTemp = moduleParts[currentPart];

                ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.1f, 0.5f, 2);
                ListenNotes.GetComponent<TextMesh>().text = "Well done";
                Debug.LogFormat(@"[Melody Sequencer #{0}] You successfully swapped slot {1} with slot {2}.", moduleId, selectedPart + 1, currentPart + 1);

                moduleParts[currentPart] = parts[currentPart];
                moduleParts[selectedPart] = modulePartsTemp;
                displayCoroutine = StartCoroutine(DisableText());
            }
            else
            {
                GetComponent<KMBombModule>().HandleStrike();
                ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
                ListenNotes.GetComponent<TextMesh>().text = "Wrong";
                Debug.LogFormat(@"[Melody Sequencer #{0}] You tried to swap slot {1} with slot {2} — strike!", moduleId, selectedPart + 1, currentPart + 1);
                displayCoroutine = StartCoroutine(DisableText());
            }
            moveActive = false;
        }
    }

    void RecordInput(int keyPressed)
    {
        displayCoroutine = StartCoroutine(DisplayFlash(keyPressed));

        if (keyPressed == parts[currentPart][keysPressed])
        {
            keysPressed++;
            if (keysPressed == 8)
            {
                ListenNotes.GetComponent<TextMesh>().color = new Color32(230, 255, 0, 255);
                ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.1f, 0.5f, 2);
                ListenNotes.GetComponent<TextMesh>().text = "Well done";
                displayCoroutine = StartCoroutine(DisableText());
                keysPressed = 0;
                moduleParts[currentPart] = parts[currentPart];
                recordActive = false;
                partsCreated++;
                if (partsCreated == 4)
                    StartCoroutine(Pass());
            }
        }
        else
        {
            Debug.LogFormat(@"[Melody Sequencer #{0}] For part {1}, you entered {2} but I expected {3}", moduleId, currentPart + 1,
                string.Join(", ", parts[currentPart].Take(keysPressed).Concat(new[] { keyPressed }).Select(note => noteNames[note]).ToArray()),
                string.Join(", ", parts[currentPart].Take(keysPressed + 1).Select(note => noteNames[note]).ToArray()));

            ListenNotes.GetComponent<TextMesh>().color = new Color32(230, 255, 0, 255);
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Wrong";
            displayCoroutine = StartCoroutine(DisableText());
            GetComponent<KMBombModule>().HandleStrike();
            keysPressed = 0;
            recordActive = false;
        }
    }

    private IEnumerator Pass()
    {
        moduleSolved = true;
        yield return new WaitForSeconds(1f);

        ListenNotes.GetComponent<TextMesh>().color = new Color32(24, 229, 24, 255);
        ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.12f, 0.5f, 2);
        ListenNotes.GetComponent<TextMesh>().text = "Melody";
        ListenNotes.SetActive(true);

        for (int i = 0; i < parts.Length; i++)
        {
            Part.GetComponent<TextMesh>().text = (i + 1).ToString();
            for (int j = 0; j < parts[i].Length; j++)
            {
                Audio.PlaySoundAtTransform(actualNoteNames[currentInst][parts[i][j]], transform);
                if (noteNames[parts[i][j]].Contains("#"))
                {
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[1];
                    yield return new WaitForSeconds(0.23f);
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[1];
                }
                else
                {
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[0];
                    yield return new WaitForSeconds(0.23f);
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[0];
                }
            }
        }

        GetComponent<KMBombModule>().HandlePass();
        StopAllCoroutines();
        solveAnimationDone = true;
    }

    private IEnumerator DisableText()
    {
        if (displayCoroutine != null)
            StopCoroutine(displayCoroutine);
        yield return new WaitForSeconds(1f);
        ListenNotes.SetActive(false);
        ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.15f, 0.5f, 2);
    }

    private IEnumerator KeyFlash(int keyPressed)
    {
        Audio.PlaySoundAtTransform(actualNoteNames[currentInst][keyPressed], transform);

        if (noteNames[keyPressed].Contains("#"))
        {
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[1];
            yield return new WaitForSeconds(0.23f);
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[1];
        }
        else
        {
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[0];
            yield return new WaitForSeconds(0.23f);
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[0];
        }
    }

    private IEnumerator DisplayFlash(int keyPressed)
    {
        if (displayCoroutine != null)
            StopCoroutine(displayCoroutine);
        ListenNotes.GetComponent<TextMesh>().text = noteNames[keyPressed];
        ListenNotes.SetActive(true);
        yield return new WaitForSeconds(0.73f);
        ListenNotes.SetActive(false);
    }

    private IEnumerator Play()
    {
        listenActive = true;
        for (int i = 0; i < moduleParts[currentPart].Length; i++)
        {
            Audio.PlaySoundAtTransform(actualNoteNames[currentInst][moduleParts[currentPart][i]], transform);
            ListenNotes.GetComponent<TextMesh>().text = noteNames[moduleParts[currentPart][i]];
            ListenNotes.SetActive(true);
            if (noteNames[moduleParts[currentPart][i]].Contains("#"))
            {
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[1];
                yield return new WaitForSeconds(0.23f);
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[1];
            }
            else
            {
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[0];
                yield return new WaitForSeconds(0.23f);
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[0];
            }
            ListenNotes.SetActive(false);
        }
        listenActive = false;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} slot 4 [select slot 4] | !{0} play 4 [select slot 4 and play it] | !{0} move to 4 [move the current selected slot to slot 4] | !{0} record C#4 D#4 F4 [press record and play these notes] | !{0} play C#4 D#4 F4 [just play these notes] | !{0} music/piano/xylo/harp [select music/piano/xylo/harp as instrument]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        while (listenActive)
            yield return "trycancel";

        if (moduleSolved)
        {
            yield return "sendtochaterror The module has entered its Melody state, causing the module to be solved shortly.";
            yield break;
        }

        Match m;
        if ((m = Regex.Match(command, @"^\s*(slot|select)\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            if (recordActive)
            {
                yield return "sendtochaterror Finish your recording first.";
                yield break;
            }
            var slotNumber = int.Parse(m.Groups[2].Value);
            if (slotNumber < 1 || slotNumber > 8)
                yield break;
            yield return null;
            yield return Enumerable.Repeat(CycleBtns[1], ((slotNumber - 1) - currentPart + 8) % 8).ToArray();
        }
        else if ((m = Regex.Match(command, @"^\s*(play|listen +to)\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            if (recordActive)
            {
                yield return "sendtochaterror Finish your recording first.";
                yield break;
            }
            var slotNumber = int.Parse(m.Groups[2].Value);
            if (slotNumber < 1 || slotNumber > 8)
                yield break;
            yield return null;
            yield return Enumerable.Repeat(CycleBtns[1], ((slotNumber - 1) - currentPart + 8) % 8).Concat(new[] { listen }).ToArray();
        }
        else if ((m = Regex.Match(command, @"^\s*(move|yellow|move +to)\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            if (recordActive)
            {
                yield return "sendtochaterror Finish your recording first.";
                yield break;
            }
            var slotNumber = int.Parse(m.Groups[2].Value);
            if (slotNumber < 1 || slotNumber > 8)
                yield break;
            yield return null;
            yield return new[] { move }.Concat(Enumerable.Repeat(CycleBtns[1], ((slotNumber - 1) - currentPart + 8) % 8)).Concat(new[] { move }).ToArray();
        }
        else if ((m = Regex.Match(command, @"^\s*(record|submit|input|enter|red|play|press)\s+([ABCDEFG#♯45 ,;]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var sequence = m.Groups[2].Value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var keysToPress = new List<KMSelectable>();
            if (m.Groups[1].Value != "press" && m.Groups[1].Value != "play")
                keysToPress.Add(record);
            for (int i = 0; i < sequence.Length; i++)
            {
                var ix = Array.IndexOf(noteNames, sequence[i].ToUpperInvariant().Replace("♯", "#"));
                if (ix == -1)
                    yield break;
                keysToPress.Add(keys[ix]);
            }
            yield return null;
            yield return "solve";
            foreach (var key in keysToPress)
            {
                yield return new[] { key };
                yield return new WaitForSeconds(.13f);
            }
            yield return "solve";
        }
        else if ((m = Regex.Match(command, @"^\s*(music|piano|xylo|harp)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            yield return null;
            var inst = m.Groups[1].Value.ToString();
            switch (inst)
            {
                case "music":
                    InstBtns[0].OnInteract();
                    break;
                case "piano":
                    InstBtns[1].OnInteract();
                    break;
                case "xylo":
                    InstBtns[2].OnInteract();
                    break;
                case "harp":
                    InstBtns[3].OnInteract();
                    break;
            }
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat(@"[Melody Sequencer #{0}] This module was force solved by TP.", moduleId);

        while (!moduleSolved)
        {
            while (listenActive)
                yield return true;

            // Are we currently recording?
            if (recordActive)
            {
                keys[parts[currentPart][keysPressed]].OnInteract();
                yield return new WaitForSeconds(.23f);
            }

            // Are we currently moving?
            else if (moveActive)
            {
                // Move it to the correct slot
                var correctSlot = Array.IndexOf(parts, moduleParts[selectedPart]);
                if (correctSlot == -1)
                {
                    // The module is in a state in which a strike is unavoidable.
                    yield break;
                }
                while (currentPart != correctSlot)
                {
                    CycleBtns[1].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                move.OnInteract();
                yield return new WaitForSeconds(.23f);
            }

            else if (moduleParts[currentPart] == null && !givenParts.Contains(currentPart))
            {
                // Start a recording
                record.OnInteract();
                yield return new WaitForSeconds(.1f);
            }

            else if (moduleParts[currentPart] != null && moduleParts[currentPart] != parts[currentPart])
            {
                // Start a move
                move.OnInteract();
                yield return new WaitForSeconds(.1f);
            }

            else
            {
                // Move on to the next part
                CycleBtns[1].OnInteract();
                yield return true;
                yield return new WaitForSeconds(.1f);
            }
        }

        while (!solveAnimationDone)
            yield return true;
    }
}