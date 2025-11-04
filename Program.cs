namespace Hw21StructScopeAnalyzer
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;

    class Program
    {
        static void Main(string[] args)
        {
            // ReportAssemblyのフォルダパス
            var reportFolder = ConfigurationManager.AppSettings["ReportFolder"];

            // hw21plusのフォルダパス
            var webFolder = ConfigurationManager.AppSettings["WebFolder"];

            // BaseReport派生クラスの抽出結果ファイル
            var reportOutputPath = ConfigurationManager.AppSettings["ReportOutputPath"];

            // BaseReport派生クラスのインスタンス生成を行っているaspx.csの結果ファイル
            var usageOutputPath = ConfigurationManager.AppSettings["UsageOutputPath"];

            var reportClassNames = new List<(string ClassName, string FilePath)>();

            // ① 帳票クラスの抽出
            var csFiles = Directory.GetFiles(reportFolder, "*.cs", SearchOption.AllDirectories)
                                   .Where(f => !f.EndsWith(".aspx.cs", StringComparison.OrdinalIgnoreCase))
                                   .ToList();

            foreach (var file in csFiles)
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classNode in classNodes)
                {
                    // 基底クラスがBaseReport
                    var baseType = classNode.BaseList?.Types.FirstOrDefault()?.ToString();
                    if (baseType != null && baseType.EndsWith("BaseReport"))
                    {
                        // InitializeComponentが呼び出されている
                        var hasInit = classNode.DescendantNodes()
                            .OfType<InvocationExpressionSyntax>()
                            .Any(inv => inv.Expression.ToString().Contains("InitializeComponent"));

                        if (hasInit)
                        {
                            var className = classNode.Identifier.Text;
                            reportClassNames.Add((className, file));
                            Console.WriteLine($"帳票クラス検出: {className}（{file}）");
                        }
                    }
                }
            }

            // 出力①：帳票クラス一覧
            var reportLines = reportClassNames
                .Select(r => $"{r.ClassName}\t{r.FilePath}")
                .ToList();
            File.WriteAllLines(reportOutputPath, reportLines, System.Text.Encoding.UTF8);
            Console.WriteLine($"帳票クラス一覧出力: {reportOutputPath}");

            // ② .aspx.cs ファイルでの使用箇所を検索
            var usageResults = new List<(string ClassName, string CsPath, string AspxPath)>();
            var aspxFiles = Directory.GetFiles(webFolder, "*.aspx.cs", SearchOption.AllDirectories).ToList();

            foreach (var file in aspxFiles)
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                foreach (var (className, _) in reportClassNames)
                {
                    var found = root.DescendantNodes()
                        .OfType<ObjectCreationExpressionSyntax>()
                        .Any(n => n.Type.ToString().EndsWith(className));

                    if (found)
                    {
                        string csFileName = Path.GetFileName(file);
                        string csFolder = Path.GetDirectoryName(file);
                        string matchedAspxPath = null;

                        var aspxCandidates = Directory.GetFiles(csFolder, "*.aspx", SearchOption.TopDirectoryOnly);
                        foreach (var aspxFile in aspxCandidates)
                        {
                            var aspxContent = File.ReadAllText(aspxFile);
                            if (aspxContent.Contains($"CodeFile=\"{csFileName}\""))
                            {
                                matchedAspxPath = aspxFile;
                                break;
                            }
                        }

                        usageResults.Add((className, file, matchedAspxPath ?? "なし"));
                        Console.WriteLine($"使用検出: {className} → {file}");
                        if (matchedAspxPath != null)
                        {
                            Console.WriteLine($"対応 .aspx ファイル: {matchedAspxPath}");
                        }
                    }
                }
            }

            // 出力②：使用箇所一覧
            var usageLines = usageResults
                .Select(u => $"{u.ClassName}\t{u.CsPath}\t{u.AspxPath}")
                .ToList();
            File.WriteAllLines(usageOutputPath, usageLines, System.Text.Encoding.UTF8);
            Console.WriteLine($"使用箇所一覧出力: {usageOutputPath}");
            Console.WriteLine("解析完了");
        }
    }
}
