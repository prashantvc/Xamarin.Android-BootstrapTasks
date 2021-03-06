﻿using System;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tools.BootstrapTasks
{
    public class CheckAdbTarget : Adb
    {
        public string SdkVersion { get; set; }

        [Output]
        public string AdbTarget { get; set; }

        [Output]
        public bool IsValidTarget { get; set; }

        protected override bool LogTaskMessages
        {
            get { return false; }
        }

        enum CommandState
        {
            CheckSdk,
            CheckPM,
        }

        CommandState state;

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Low, $"Task {nameof(CheckAdbTarget)}");
            Log.LogMessage(MessageImportance.Low, $"  {nameof(AdbTarget)}: {AdbTarget}");
            Log.LogMessage(MessageImportance.Low, $"  {nameof(SdkVersion)}: {SdkVersion}");

            state = CommandState.CheckSdk;
            base.Execute();

            if (IsValidTarget)
            {
                state = CommandState.CheckPM;
                base.Execute();
            }

            Log.LogMessage(MessageImportance.Low, $"  [Output] {nameof(AdbTarget)}: {AdbTarget}");
            Log.LogMessage(MessageImportance.Low, $"  [Output] {nameof(IsValidTarget)}: {IsValidTarget}");

            return true;
        }

        protected override bool HandleTaskExecutionErrors()
        {
            // We ignore all generated errors
            return true;
        }

        protected override string GenerateCommandLineCommands()
        {
            switch (state)
            {
                case CommandState.CheckSdk:
                    return $"{AdbTarget} shell getprop ro.build.version.sdk";
                case CommandState.CheckPM:
                    return $"{AdbTarget} shell pm path com.android.shell";
            }
            throw new InvalidOperationException($"Unknown state `{state}`!");
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            base.LogEventsFromTextOutput(singleLine, messageImportance);
            if (string.IsNullOrEmpty(singleLine))
                return;

            switch (state)
            {
                case CommandState.CheckSdk:
                    CheckSdkOutput(singleLine, messageImportance);
                    break;
                case CommandState.CheckPM:
                    CheckPMOutput(singleLine, messageImportance);
                    break;
            }
        }

        void CheckSdkOutput(string singleLine, MessageImportance messageImportance)
        {
            if (singleLine.Equals("List of devices attached", StringComparison.OrdinalIgnoreCase))
                return;
            // Ignore stderr
            if (messageImportance == MessageImportance.High)
                return;
            // Informational messages, e.g.
            //  * daemon not running. starting it now on port 5037 *
            //  * daemon started successfully *
            if (singleLine.StartsWith("* ", StringComparison.Ordinal))
                return;
            // Error messages, e.g.: error: device '(null)' not found
            if (singleLine.StartsWith("error: ", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrEmpty(SdkVersion))
            {
                IsValidTarget = true;
                return;
            }
            if (string.Equals(SdkVersion, singleLine, StringComparison.OrdinalIgnoreCase))
            {
                IsValidTarget = true;
                return;
            }
            int required, target;
            if (int.TryParse(SdkVersion, out required) &&
                    int.TryParse(singleLine, out target) &&
                    target >= required)
            {
                IsValidTarget = true;
                return;
            }
        }

        void CheckPMOutput(string singleLine, MessageImportance messageImportance)
        {
            if (singleLine.Contains("Error: Could not access the Package Manager") ||
                    singleLine.Contains("Failure "))
            {
                IsValidTarget = false;
                return;
            }
        }
    }
}

