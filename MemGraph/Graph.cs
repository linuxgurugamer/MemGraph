/*
 Copyright (c) 2016 Gerry Iles (Padishar)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in
 all copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 THE SOFTWARE.
*/

using System;
//using System.IO;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using KSP;
using KSP.IO;


namespace MemGraph
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    public class Graph : MonoBehaviour
    {
        const String testFilename = "test.cfg";

        const int GraphX = 10;
        const int GraphY = 36;
        const int GraphWidth = 600;
        const int GraphHeight = 256;
        const int LabelX = 10;
        const int LabelY = 18;
        const int LabelWidth = GraphWidth;
        const int LabelHeight = 20;
        const int WndWidth = GraphWidth + 8;
        const int WndHeight = GraphHeight + 42;

        const int numScales = 15;   // Number of entries in the scale array
        const int kb = 1024;
        const int mb = kb * kb;

        LogMsg Log;

        PadHeap padHeap;

        Rect windowPos;
        Rect windowDragRect;
        Rect helpWinPos;
        int windowId = 0;
        string windowTitle;
        bool showUI = true;
        bool showHelp = false;
        Rect labelRect;
        Rect graphRect;

        long[] values;
        bool[] flags;
        Texture2D texGraph;

        int valIndex = 0;           // The current index into the values array
        int lastRendered = 0;       // The last index of the values array that has been rendered into the texture

        int lastColCount = 0;       // The most recent count of GC runs
        long lastAlloc = 0;         // The most recent value of total memory
        long lastAllocMB = 0;       // The most recent value of total memory in MB displayed in window

        long totalAlloc = 0;        // The sum of all the memory deltas (this is the value stored in the array every ~1s)
        bool doneGC = false;        // Has a GC run (this is also stored)

        long lastValue = 0;         // The last value stored in the array
        int lastFixedCount = 0;     // The last value of fixedCount used to build guiStr
        int lastUpdateCount = 0;    // The last value of updateCount used to build guiStr
        long lastMinHeapMB = 0;     // The heap size after the last GC run
        long lastMaxHeapMB = 0;     // The heap size immediately before the last GC run
        long lastGCInterval = 0;

        int fixedCount = 0;         // The count of FixedUpdate calls in the current interval
        int updateCount = 0;        // The count of Update calls in the current interval
        long minHeap = 0;           // The heap size after the last GC run
        long maxHeap = 0;           // The heap size immediately before the last GC run

        string guiStr;              // The string at the top of the window (only updated when required)

        long prevGCTime = 0;

        long startTime;             // The timestamp totalAlloc was last stored
        long ticksPerSec;           // The number of timestamp ticks in a second

        bool fullUpdate = true;     // Flag to force re-render of entire texture (e.g. when changing scale)

        bool startVisible = true;
        bool applyPadding = false;

        KeyCode keyToggleWindow;
        KeyCode keyScaleUp;
        KeyCode keyScaleDown;
        KeyCode keyRunTests;
        KeyCode keyPadHeap;
        KeyCode keyMark;
        KeyCode keyToggleLogging;

        bool enableLogging = false;

        int scaleIndex = 4;         // Index of the current vertical scale
        static double[] valCycle;
        static string[] valCycleStr;

        static string[] hexchars;

        Color[] blackLine;
        Color[] redLine;
        Color[] greenLine;
        Color[] blueLine;

        static StringBuilder strBuild;

        GUIStyle labelStyle;

        GUI.WindowFunction wndFunction = null;

        static Graph instance = null;

        public static bool IsOpen()
        {
            return instance != null ? instance.showUI : false;
        }

        void Awake()
        {
            if (instance != null)
            {
                gameObject.DestroyGameObject();
                return;
            }

            DontDestroyOnLoad(gameObject);

            instance = this;
            ROOT_PATH = KSPUtil.ApplicationRootPath;
            windowId = Guid.NewGuid().GetHashCode();
            windowTitle = "MemGraph";

            strBuild = new StringBuilder(128);
            Log = new LogMsg();

            padHeap = new PadHeap();

            valCycle = new double[] { 64 * kb, 128 * kb, 256 * kb, 512 * kb, 1 * mb, 2 * mb, 4 * mb, 8 * mb, 16 * mb, 32 * mb, 64 * mb, 128 * mb, 256 * mb, 512 * mb, 1024*mb };
            valCycleStr = new string[] { "64 KB", "128 KB", "256 KB", "512 KB", "1 MB", "2 MB", "4 MB", "8 MB", "16 MB", "32 MB", "64 MB", "128 MB", "256 MB", "512 MB", "1 GB" };

            hexchars = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F" };

            windowPos.Set(80, 80, WndWidth, WndHeight);
            windowDragRect.Set(0, 0, WndWidth, WndHeight);
            labelRect.Set(LabelX, LabelY, LabelWidth, LabelHeight);
            graphRect.Set(GraphX, GraphY, GraphWidth, GraphHeight);

            helpWinPos.Set(40, 40, 500, 300);

            values = new long[GraphWidth];
            flags = new bool[GraphWidth];
            texGraph = new Texture2D(GraphWidth, GraphHeight, TextureFormat.ARGB32, false);

            redLine = new Color[GraphHeight];
            greenLine = new Color[GraphHeight];
            blueLine = new Color[GraphHeight];
            blackLine = new Color[GraphHeight];
            for (int i = 0; i < blackLine.Length; i++)
            {
                blackLine[i] = Color.black;
                redLine[i] = Color.red;
                greenLine[i] = Color.green;
                blueLine[i] = Color.blue;
            }

            for (int j = 0; j < GraphWidth; j++)
                values[j] = 0;

            ReadSettings();
            showUI = startVisible;

            UpdateGuiStr();

            GameEvents.onLevelWasLoaded.Add(HandleLevelWasLoaded);

            lastColCount = GC.CollectionCount(GC.MaxGeneration);
            lastAlloc = GC.GetTotalMemory(false);
            lastAllocMB = lastAlloc >> 20;

            startTime = Stopwatch.GetTimestamp();
            ticksPerSec = Stopwatch.Frequency;

            // Force a full update of the graph texture
            fullUpdate = true;
        }

        void Start()
        {
            if (applyPadding)
                padHeap.Pad();
            areaStyle = new GUIStyle(HighLogic.Skin.textArea);
            areaStyle.richText = true;
        }

        void HandleLevelWasLoaded(GameScenes scene)
        {
            ReadSettings();
        }

        void ReadSettings()
        {
            const string settingsFile = "settings.cfg";

            if (File.Exists<Graph>(settingsFile))
            {
                String[] lines = File.ReadAllLines<Graph>(settingsFile);

                for (int i = 0; i < lines.Length; i++)
                {
                    String[] line = lines[i].Split('=');
                    if (line.Length == 2)
                    {
                        String key = line[0].Trim();
                        String val = line[1].Trim();
                        if (key == "visible")
                            ReadBool(val, ref startVisible);
                        else if (key == "applyPadding")
                            ReadBool(val, ref applyPadding);
                        else if (key == "keyToggleWindow")
                            ReadKeyCode(val, ref keyToggleWindow, KeyCode.KeypadMultiply);
                        else if (key == "keyScaleUp")
                            ReadKeyCode(val, ref keyScaleUp, KeyCode.KeypadPlus);
                        else if (key == "keyScaleDown")
                            ReadKeyCode(val, ref keyScaleDown, KeyCode.KeypadMinus);
                        else if (key == "keyRunTests")
                            ReadKeyCode(val, ref keyRunTests, KeyCode.KeypadDivide);
                        else if (key == "keyPadHeap")
                            ReadKeyCode(val, ref keyPadHeap, KeyCode.End);
                        else if (key == "enableLogging")
                            ReadBool(val, ref enableLogging);
                        else if (key == "keyMark")
                            ReadKeyCode(val, ref keyMark, KeyCode.Home);
                        else if (key == "keyToggleLogging")
                            ReadKeyCode(val, ref keyToggleLogging, KeyCode.PageUp);                        
                        else
                        {
                            Log.buf.Append("Ignoring invalid key in settings: '");
                            Log.buf.Append(lines[i]);
                            Log.buf.AppendLine("'");
                        }

                    }
                    else
                    {
                        Log.buf.Append("Ignoring invalid line in settings: '");
                        Log.buf.Append(lines[i]);
                        Log.buf.AppendLine("'");
                    }
                }
            }
        }

        void AddMemoryIncrement()
        {
            long endTime = Stopwatch.GetTimestamp();

            long currentMem = GC.GetTotalMemory(false);
            int colCount = GC.CollectionCount(GC.MaxGeneration);
            if (lastColCount != colCount)
            {
                doneGC = true;
                lastColCount = colCount;
                minHeap = currentMem;     // Store current as min
                maxHeap = lastAlloc;

                lastGCInterval = (long)(((double)(endTime - prevGCTime) / (double)ticksPerSec) + 0.5d);
                prevGCTime = endTime;
            }

            // If the GC has run then the total memory may have shrunk so only add if it increases
            long diff = currentMem - lastAlloc;
            if (diff > 0)
                totalAlloc += diff;

            // Remember the current memory for next time
            lastAlloc = currentMem;

            long timeDelta = endTime - startTime;
            if (timeDelta > ticksPerSec)
            {
                // At least 1 second has passed so record the values
                values[valIndex] = totalAlloc;
                flags[valIndex] = doneGC;

                // Calculate the new lastAllocMB value
                long newMB = lastAlloc >> 20;

                lastAllocMB = newMB;
                lastValue = totalAlloc;
                lastFixedCount = fixedCount;
                lastUpdateCount = updateCount;
                lastMinHeapMB = minHeap >> 20;
                lastMaxHeapMB = maxHeap >> 20;
                UpdateGuiStr();

                // Reset the values for the next accumulation
                startTime = endTime;
                totalAlloc = 0;
                fixedCount = 0;
                updateCount = 0;
                doneGC = false;

                // Increament the current value index and force full update if we have caught up with the rendering
                valIndex = (valIndex + 1) % GraphWidth;
                if (valIndex == lastRendered)
                    fullUpdate = true;
            }
        }

        const string separator = ",";
        string ROOT_PATH;
        string filePrefix = "dataLog";
        string fileSuffix = ".txt";
        //bool unixFormat = false;
        string eol = "\r\n";
        string singleLineStr;
        bool header = false;

        public void WriteFile(string fileName, string value, bool singleLine)
        {
            {
                string fname = ROOT_PATH + filePrefix + "." + fileName + fileSuffix;
                
                if (singleLine)
                    System.IO.File.WriteAllText(fname, value + eol);
                else
                    System.IO.File.AppendAllText(fname, value + eol);
            }
            singleLineStr += value + separator;
        }

        void UpdateGuiStr()
        {
            // We use a static StringBuilder to do this to avoid as much garbage as possible
            strBuild.Length = 0;
            strBuild.Append("Scale:");
            strBuild.Append(valCycleStr[scaleIndex]);
            strBuild.Append("   Heap Min:");
            strBuild.Append(lastMinHeapMB);
            strBuild.Append("   Cur:");
            strBuild.Append(lastAllocMB);
            strBuild.Append("   Max:");
            strBuild.Append(lastMaxHeapMB);
            strBuild.Append(" MB   Last:");
            strBuild.Append(lastValue / 1024);
            strBuild.Append(" KB   R:");
            strBuild.Append(lastUpdateCount);
            strBuild.Append("   P:");
            strBuild.Append(lastFixedCount);
            strBuild.Append("   Int:");
            strBuild.Append(lastGCInterval);
            strBuild.Append(" s");
            guiStr = strBuild.ToString();

            if (enableLogging)
            {
                // Now write to log files
                // The first set are to individual files, this is for instant viewing
                if (!header)
                {
                    header = true;
                    singleLineStr = "HeapMin, Cur, Max, Last, R, P, Interval";
                    WriteFile("Log", singleLineStr, true);
                }
                singleLineStr = "";
                WriteFile("HeapMin", lastMinHeapMB.ToString(), true);
                WriteFile("Cur", lastAllocMB.ToString(), true);
                WriteFile("Max", lastMaxHeapMB.ToString(), true);
                WriteFile("Last", (lastValue / 1024).ToString(), true);
                WriteFile("R", lastUpdateCount.ToString(), true);
                WriteFile("P", lastFixedCount.ToString(), true);
                WriteFile("Interval", lastGCInterval.ToString(), true);
                // Now write all the values to a log file
                WriteFile("Log", singleLineStr, false);
            }
        }
        int markCnt = 0;
        void WriteMarkToLog()
        {
            if (enableLogging)
            {
                markCnt++;
                WriteFile("Log", "***** Mark # " + markCnt + " *****", false);
                ScreenMessages.PostScreenMessage("MemGraph: Mark # " + markCnt + " written to log", 5f);
            }
        }
        void FixedUpdate()
        {
            fixedCount += 1;
            AddMemoryIncrement();
        }

        void Update()
        {
            //print("Update Start");
            updateCount += 1;
            AddMemoryIncrement();

            if (GameSettings.MODIFIER_KEY.GetKey())
            {
                if (Input.GetKeyDown(keyPadHeap))
                {
                    padHeap.Pad();
                }
                if (Input.GetKeyDown(keyRunTests))
                {
                    RunTestCode();
                }
                if (Input.GetKeyDown(keyToggleWindow))
                {
                    showUI = !showUI;
                }
                if (Input.GetKeyDown(keyScaleUp))
                {
                    // Increase scale
                    scaleIndex = (scaleIndex + 1) % numScales;
                    UpdateGuiStr();
                    fullUpdate = true;
                }
                if (Input.GetKeyDown(keyScaleDown))
                {
                    // Decrease scale
                    scaleIndex = (scaleIndex + numScales - 1) % numScales;
                    UpdateGuiStr();
                    fullUpdate = true;
                }
                if (Input.GetKeyDown(keyMark))
                {
                    WriteMarkToLog();
                }
                if (Input.GetKeyDown(keyToggleLogging))
                {
                    enableLogging = !enableLogging;
                    if (enableLogging)
                        ScreenMessages.PostScreenMessage("MemGraph: Logging enabled", 5f);
                    else
                        ScreenMessages.PostScreenMessage("MemGraph: Logging disabled", 5f);
                }
            }

            if (!showUI)
                return;

            if (fullUpdate)
            {
                fullUpdate = false;
                lastRendered = (valIndex + 1) % GraphWidth;
            }

            // If we want to update this time
            if (lastRendered != valIndex)
            {
                double scale = valCycle[scaleIndex];

                // We're going to wrap this back round to the start so copy the value so 
                int startlastRend = lastRendered;

                // Update the columns from lastRendered to valIndex wrapping round at the end
                if (startlastRend >= valIndex)
                {
                    for (int x = startlastRend; x < GraphWidth; x++)
                    {
                        DrawColumn(texGraph, x, (int)((double)values[x] * GraphHeight / scale), greenLine, flags[x] ? redLine : blackLine);
                    }

                    startlastRend = 0;
                }

                for (int x = startlastRend; x < valIndex; x++)
                {
                    DrawColumn(texGraph, x, (int)((double)values[x] * GraphHeight / scale), greenLine, flags[x] ? redLine : blackLine);
                }

                if (valIndex < GraphWidth)
                    texGraph.SetPixels(valIndex, 0, 1, GraphHeight, blueLine);
                if (valIndex != GraphWidth - 2)
                    texGraph.SetPixels((valIndex + 1) % GraphWidth, 0, 1, GraphHeight, blackLine);
                texGraph.Apply();

                lastRendered = valIndex;
            }
            //print("Update End");
        }

        void DrawColumn(Texture2D tex, int x, int y, Color[] fgcol, Color[] bgcol)
        {
            //print("drawcol(" + x + ", " + y + ")");
            if (y > GraphHeight - 1)
                y = GraphHeight - 1;
            tex.SetPixels(x, 0, 1, y + 1, fgcol);
            if (y < GraphHeight - 1)
                tex.SetPixels(x, y + 1, 1, GraphHeight - 1 - y, bgcol);
        }

        void OnGUI()
        {
            if (wndFunction == null)
                wndFunction = new GUI.WindowFunction(WindowGUI);

            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label);

            if (showUI)
                windowPos = GUI.Window(windowId, windowPos, wndFunction, windowTitle);

            if (showHelp)
                helpWinPos = GUILayout.Window(windowId + 1, helpWinPos, helpWin, "MemGraph Help");
        }

        void WindowGUI(int windowID)
        {
            if (GUI.Button(new Rect(helpWinPos.width - 48, 2, 18, 15), "?"))
                showHelp = !showHelp;

            GUI.Label(labelRect, guiStr, labelStyle);
            GUI.Box(graphRect, texGraph, labelStyle);
            GUI.DragWindow(windowDragRect);
        }
        static GUIStyle areaStyle;
        const string helpText =
            "<B><color=yellow>General Controls</color></B>\n\n" +
            "<B>Mod-KeypadMultiply</B> toggles the display of the window.\n" +
            "<B>Mod-KeypadPlus</B> increases the vertical scale of the graph.\n" +
            "<B>Mod-KeypadMinus</B> decreases the vertical scale of the graph.\n" +
            "<B>Mod-KeypadDivide</B> runs a bit of test code controlled by MemGraph\\PluginData\\test.cfg\n" +
            "<B>Mod-End</B> pads the Mono heap with a configurable amount of headroom to reduce frequency of garbage collections.\n\n" +

            "<B><color=yellow>Logging Controls</color></B>\n\n" +

            "<B>Mod-Home</B> writes a marker to the log file(if logging is enabled, see below)\n" +
            "<B>Mod-PageUp</B> toggles logging\n\n" +

            "<B><color=yellow>Legend</color>\n\n</B>\b" +
            "<color=green>Green</color>     Memory﻿ allocation﻿\n" +
            "<color=red>Red background</color> Garbage collection";


        void helpWin(int windowID)
        {
            GUILayout.BeginHorizontal();
            GUILayout.TextArea(helpText, areaStyle);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close"))
                showHelp = false;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

   
            GUI.DragWindow();
        }

        void RunTestCode()
        {
            try
            {

                int NumBlocks = 8;
                int BlockSize = 4;

                if (File.Exists<Graph>(testFilename))
                {
                    String[] lines = File.ReadAllLines<Graph>(testFilename);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        String[] line = lines[i].Split('=');
                        if (line.Length == 2)
                        {
                            String key = line[0].Trim();
                            String val = line[1].Trim();
                            if (key == "size")
                                ReadInt32(val, ref BlockSize);
                            else if (key == "num")
                                ReadInt32(val, ref NumBlocks);
                        }
                        else
                        {
                            Log.buf.Append("Ignoring invalid line in settings: '");
                            Log.buf.Append(lines[i]);
                            Log.buf.AppendLine("'");
                        }
                    }
                }
                else
                    Log.buf.AppendLine("Can't find test file");

                Log.buf.Append("MemGraph Test(");
                Log.buf.Append(NumBlocks);
                Log.buf.Append(", ");
                Log.buf.Append(BlockSize);
                Log.buf.AppendLine(")");

                long startMem = GC.GetTotalMemory(false);
                int startCount = GC.CollectionCount(GC.MaxGeneration);

                Log.buf.Append("Initial memory = ");
                Log.buf.Append(startMem);
                Log.buf.Append("  (counts = ");
                Log.buf.Append(startCount);
                Log.buf.AppendLine(")");

                long lastMem = startMem;
                byte[] block;
                for (int i = 0; i < NumBlocks; i++)
                {
                    // Allocate a block
                    block = new byte[BlockSize];

                    int len = block.Length;

                    // Dump out the 64 bytes before &block[0]
                    if (i == 0)
                    {
                        unsafe
                        {
                            fixed (byte* ptr = &block[0])
                            {
                                Log.buf.Append("&block[0] = ");
                                Log.buf.Append((long)ptr);
                                Log.buf.AppendLine("");
                                for (byte* p = ptr - 64; p < ptr; p += 16)
                                {
                                    for (byte* q = p; q < p + 16; q++)
                                    {
                                        byte val = *q;
                                        Log.buf.Append(hexchars[(val >> 4) & 15]);
                                        Log.buf.Append(hexchars[val & 15]);
                                        Log.buf.Append(" ");
                                    }
                                    Log.buf.AppendLine("");
                                }
                            }
                        }
                    }

                    // If a GC has run then abort
                    int curCount = GC.CollectionCount(GC.MaxGeneration);
                    if (curCount != startCount)
                    {
                        Log.buf.AppendLine("GC has run, aborting");
                        break;
                    }

                    long curMem = GC.GetTotalMemory(false);
                    if (curMem != lastMem)
                    {
                        Log.buf.Append("Block ");
                        Log.buf.Append(i);
                        Log.buf.Append(" increase = ");
                        Log.buf.Append(curMem - lastMem);
                        Log.buf.AppendLine("");

                        lastMem = curMem;
                    }
                }

                long endMem = GC.GetTotalMemory(false);
                int endCount = GC.CollectionCount(GC.MaxGeneration);
                Log.buf.Append("Final memory = ");
                Log.buf.Append(endMem);
                Log.buf.Append("  (counts = ");
                Log.buf.Append(endCount);
                Log.buf.AppendLine(")");
            }
            catch (Exception exp)
            {
                Log.buf.Append("Exception caught: ");
                Log.buf.AppendLine(exp.ToString());
            }
            Log.Flush();
        }

        void ReadBool(String val, ref bool variable)
        {
            if (val == "true")
                variable = true;
            else if (val == "false")
                variable = false;
        }

        void ReadInt32(String str, ref Int32 variable)
        {
            Int32 value = 0;
            if (Int32.TryParse(str, out value))
                variable = value;
        }

        void ReadKeyCode(String str, ref KeyCode variable, KeyCode defValue)
        {
            try
            {
                variable = (KeyCode)Enum.Parse(typeof(KeyCode), str, false);
                Log.buf.Append("Read value of:");
                Log.buf.AppendLine("" + variable);
            }
            catch (Exception exp)
            {
                Log.buf.Append("Unrecognised KeyCode: ");
                Log.buf.AppendLine(str);
                Log.buf.AppendLine(exp.ToString());
                variable = defValue;
            }
            Log.Flush();
        }

        void Trace(String message)
        {
            Log.buf.AppendLine(message);
            Log.Flush();
        }
    }
}
