using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WebStream.DLLWrapper
{    public static class BrowserInputState
    {
        // Keys currently held down (by code)
        public static readonly HashSet<string> Down = new();

        // Existing browser->DLL->Unity callback
        public static void OnBrowserMessage(string json)
        {
            // ultra-light parse (avoids full JSON libs)
            if (!json.Contains("\"type\":\"key\"")) return;

            bool isDown = json.Contains("\"action\":\"down\"");
            int i = json.IndexOf("\"code\":\"");
            if (i < 0) return;
            i += 8;
            int j = json.IndexOf('"', i);
            if (j < 0) return;
            string code = json.Substring(i, j - i);

            if (isDown) Down.Add(code);
            else Down.Remove(code);
        }

        // Helpers
        public static bool Held(string code) => Down.Contains(code);
    }

    [StructLayout(LayoutKind.Sequential)]
    public class TransformStruct
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class LogData
    {
        public TransformStruct Transform = new();
        public bool Shader1State = false;
    }

    public static class WebLoggerAPI
    {
        private const string _Dll = "WebRTCStreamer.dll";


        [DllImport(_Dll)] // Initialize sdk
        public static extern void Init();

        [DllImport(_Dll)] // Send logs to webrtc
        public static extern bool LogData(LogData data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CommandDelegate(string message);

        [DllImport(_Dll)] // Register "on message received" callback
        public static extern void RegisterCommandCallback(CommandDelegate callback);

        [DllImport(_Dll)] // Terminate sdk
        public static extern void StopSignaling();

        // Handle callback command
        public static void HandleCommand(string msg)
        {
            BrowserInputState.OnBrowserMessage(msg);
        }

        public static void Start()
        {

            Application.runInBackground = true;
            Init();
            RegisterCommandCallback(HandleCommand);
        }
    }
}
