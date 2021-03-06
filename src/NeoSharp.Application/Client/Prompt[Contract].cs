﻿using NeoSharp.Application.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;

namespace NeoSharp.Application.Client
{
    public partial class Prompt : IPrompt
    {
        /*
        TODO:
        load_run {path/to/file.avm} (test {params} {returntype} {needs_storage} {needs_dynamic_invoke} {test_params})
        import contract {path/to/file.avm} {params} {returntype} {needs_storage} {needs_dynamic_invoke}
        import contract_addr {contract_hash} {pubkey}
        */

        /// <summary>
        /// Build contract
        /// </summary>
        /// <param name="inputPath">File Input</param>
        /// <param name="fileDest">File Dest</param>
        [PromptCommand("build", Help = "Build a contract", Category = "Contracts")]
        private void BuildCommand(FileInfo inputPath, FileInfo outputPath)
        {
            if (outputPath.Exists) throw (new Exception("Output file already exists"));
            if (!inputPath.Exists) throw (new FileNotFoundException(inputPath.FullName));

            string[] dump;
            using (Process p = Process.Start(new ProcessStartInfo()
            {
                FileName = "neon",
                Arguments = "\"" + inputPath.FullName + "\"",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            }))
            {
                if (!p.Start())
                {
                    throw (new Exception("Error starting neon"));
                }

                dump = p.StandardOutput.ReadToEnd().Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                p.WaitForExit();
            }

            foreach (var line in dump)
            {
                _consoleWriter.WriteLine(line, ConsoleOutputStyle.Log);
            }

            // Looking for .abi.json

            string tempFile = Path.ChangeExtension(inputPath.FullName, ".abi.json");
            if (File.Exists(tempFile))
            {
                try
                {
                    JToken json = JToken.Parse(File.ReadAllText(tempFile));
                    _consoleWriter.WriteLine(json.ToString(Formatting.Indented), ConsoleOutputStyle.Information);
                }
                catch { }
            }

            // Looking for .avm

            tempFile = Path.ChangeExtension(inputPath.FullName, ".avm");
            if (File.Exists(tempFile))
            {
                var avm = File.ReadAllBytes(tempFile);
                if (avm != null) File.WriteAllBytes(outputPath.FullName, avm);
            }
            else
            {
                throw (new ArgumentException("Error compiling the contract"));
            }
        }
    }
}