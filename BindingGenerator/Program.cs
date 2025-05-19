using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Passes;

/// Copyright (C) 2025 Holger Förterer
/// initially based on Code by Il Harper found in a comment to Spout.Net
/// https://github.com/Ruminoid/Spout.NET/issues/1

namespace SpoutDX
{
    public class SpoutDXLibrary : ILibrary
    {
        // using Spout2, version 1.0.1
        public static readonly string SpoutPath = Path.GetFullPath(@"..\..\..\..\");
        public static readonly string WinSDKPath = @"C:\Program Files (x86)\Windows Kits\10\";
        public static readonly string[] HeaderPaths = { @$"{SpoutPath}SPOUTSDK\SpoutDirectX\SpoutDX",
                                                        @$"{SpoutPath}SPOUTSDK\SpoutGL" };
        public static readonly string[] SourcePaths = { @$"{SpoutPath}SPOUTSDK\SpoutDirectX\SpoutDX" };
        public static readonly string LatestWinSDK = GetLatestSDKDir(@$"{WinSDKPath}Include\");
        public static readonly string WinHeaderPath = @$"{WinSDKPath}Include\{LatestWinSDK}\um";
        public static readonly string[] LibraryPaths = { @$"{WinSDKPath}Lib\{LatestWinSDK}\um\x64",
                                                         @$"{SpoutPath}BUILD\Binaries\x64" };
        public static readonly string OutputPath = @$"{SpoutPath}SPOUTDXNET\SpoutDirectX\SpoutDX";

        /// Setup the driver options here.
        public void Setup(Driver driver)
        {
            // set generation options
            var options = driver.Options;
            options.GeneratorKind = GeneratorKind.CSharp;
            options.OutputDir = OutputPath;
            options.Compilation.Platform = TargetPlatform.Windows;
            options.Compilation.VsVersion = VisualStudioVersion.Latest;
            options.Compilation.Target = CompilationTarget.SharedLibrary;
            options.Compilation.DebugMode = true;
            options.CompileCode = false;
            options.GenerateFinalizers = true;
            options.GenerationOutputMode = GenerationOutputMode.FilePerModule;
            options.GenerateDebugOutput = true;

            // add DirectX3D11 module
            var moduleSpout = options.AddModule("SpoutDX");
            // add all header paths
            moduleSpout.IncludeDirs.Add(WinHeaderPath);
            foreach (var path in HeaderPaths)
                moduleSpout.IncludeDirs.Add(path);

            // add the headers themselves
            foreach (var path in HeaderPaths)
            {
                foreach (string file in Directory.GetFiles(path))
                    if (file.EndsWith(".h") || file.EndsWith(".hpp"))
                        moduleSpout.Headers.Add(file);
            }
            moduleSpout.Headers.Add($@"{WinHeaderPath}\d3d11.h");
 
            // now add the source
            foreach (var path in SourcePaths)
            {
                foreach (string file in Directory.GetFiles(path))
                    if (file.EndsWith(".cpp"))
                        moduleSpout.CodeFiles.Add(file);
            }

            // add all libraries, especially our own
            Console.WriteLine(@$"Using Windows SDK in {WinSDKPath}{LatestWinSDK}");
            moduleSpout.LibraryDirs.AddRange(LibraryPaths);
            moduleSpout.Libraries.Add(@$"Spout.lib");
            moduleSpout.Libraries.Add(@$"SpoutDX.lib");
            // moduleSpout.Libraries.Add(@$"SpoutLibrary.lib");
        }

        /// Get most up to date SDK directory given a general Windows SDK path
        static string GetLatestSDKDir(string winSDKPath)
        {
            var directory = new DirectoryInfo(winSDKPath);
            return directory.GetDirectories()
                 .OrderByDescending(f => f.Name)
                 .First().Name;
        }

        /// Setup passes
        public void SetupPasses(Driver driver)
        {
            driver.Context.TranslationUnitPasses.AddPass(new CheckKeywordNamesPass());
            driver.Context.TranslationUnitPasses.RenameDeclsUpperCase(RenameTargets.Any);
            driver.Context.TranslationUnitPasses.AddPass(new FunctionToStaticMethodPass());
            driver.Context.TranslationUnitPasses.AddPass(new HandleDefaultParamValuesPass());
            driver.Context.TranslationUnitPasses.AddPass(new CheckAmbiguousFunctions());
            driver.Context.TranslationUnitPasses.AddPass(new CheckOperatorsOverloadsPass());
            driver.Context.TranslationUnitPasses.AddPass(new CheckIgnoredDeclsPass());
            driver.Context.TranslationUnitPasses.AddPass(new CheckMacroPass());
            driver.Context.TranslationUnitPasses.AddPass(new CheckVirtualOverrideReturnCovariance());
            driver.Context.TranslationUnitPasses.AddPass(new CleanInvalidDeclNamesPass());
            // driver.Context.TranslationUnitPasses.AddPass(new CleanUnitPass());
        }

        /// Do transformations that should happen before passes are processed.
        public void Preprocess(Driver driver, ASTContext context)
        {
            // sort out all probelmatic DirectX3D11 classes
            string[] skipClass = { "ID3D11", "ID3D10", "_D3D10",
                                   "BasicString", /*"IProvideMultipleClassInfo",*/
                                   "IProvideClassInfo", "ISimpleFrameSite",
                                   "IPictureDisp", "IObjectWithSite",
                                   "IFont",
                                   "tagCALPOLESTR" };
            string[] createClass = { "ID3D11Device", "ID3D11Texture2D", "ID3D11DeviceContext",
                                     "D3D11SHADER_RESOURCE_VIEW_DESC",
                                     "D3D11UNORDERED_ACCESS_VIEW_DESC",
                                     "D3D11RENDER_TARGET_VIEW_DESC",
                                     "SpoutDX" };
            foreach (var unit in context.TranslationUnits)
            {
                foreach (var myClass in unit.Classes)
                {
                    var skip = false;
                    foreach (var s in skipClass)
                        if (myClass.Name.StartsWith(s))
                            skip = true;
                    foreach (var s in createClass)
                        if (myClass.Name.Equals(s))
                            skip = false;

                    if (skip)
                        myClass.ExplicitlyIgnore();
                }
            }
        }

        /// Do transformations that should happen after passes are processed.
        public void Postprocess(Driver driver, ASTContext ctx)
        {
            RenameTypes(ctx);
        }

        public void RenameTypes(ASTContext context)
        {
            // rename namespace "Std" to namespace "Spout.Std"
            foreach (var unit in context.TranslationUnits)
            {
                foreach (var myNamespace in unit.Namespaces)
                {
                    if (myNamespace.Name.Equals("Std"))
                    {
                        myNamespace.Name = "Spout.Std";
                    }
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            ConsoleDriver.Run(new SpoutDXLibrary());
        }
    }
}