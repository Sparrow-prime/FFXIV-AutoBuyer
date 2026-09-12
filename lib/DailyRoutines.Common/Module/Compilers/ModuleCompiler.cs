using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using OmenTools.Dalamud;

namespace DailyRoutines.Common.Module.Compilers;

public sealed class ModuleCompiler
{
    private static readonly CSharpCompilationOptions CompilationOptions = new
    (
        OutputKind.DynamicallyLinkedLibrary,
        optimizationLevel: OptimizationLevel.Release,
        assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
        concurrentBuild: true,
        allowUnsafe: true
    );

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default;

    private readonly ConcurrentDictionary<string, Lazy<byte[]>>    compiledAssemblyCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Assembly?>> loadedAssemblyCache   = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<ModuleLoadContext>              loadedContexts        = [];

    private readonly Lazy<ImmutableArray<MetadataReference>> referenceListLazy = new(BuildReferenceList, LazyThreadSafetyMode.ExecutionAndPublication);

    private ImmutableArray<MetadataReference> ReferenceList => referenceListLazy.Value;

    public void UnloadAll()
    {
        var startTimestamp     = Stopwatch.GetTimestamp();
        var compiledCacheCount = compiledAssemblyCache.Count;
        var loadedCacheCount   = loadedAssemblyCache.Count;
        var contextCount       = loadedContexts.Count;

        DLog.Debug
        (
            $"[ModuleCompiler] 开始清理编译器缓存, 已编译缓存: {compiledCacheCount}, 已加载缓存: {loadedCacheCount}, 加载上下文: {contextCount}"
        );

        compiledAssemblyCache.Clear();
        loadedAssemblyCache.Clear();

        var unloadedCount = 0;

        while (loadedContexts.TryTake(out var context))
        {
            unloadedCount++;
            TryUnloadContext(context);
        }

        DLog.Debug
        (
            $"[ModuleCompiler] 编译器缓存清理完成, 已卸载上下文: {unloadedCount}, 耗时: {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F2} ms"
        );
    }

    public Assembly? Load(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            DLog.Debug("[ModuleCompiler] 接收到空白源码, 跳过模块加载");
            return null;
        }

        DLog.Debug($"[ModuleCompiler] 接收到模块源码加载请求, 源码长度: {code.Length} 字符");

        var assemblyBytes = Compile(code);

        if (assemblyBytes.Length == 0)
        {
            DLog.Warning("[ModuleCompiler] 编译结果为空, 未执行程序集加载");
            return null;
        }

        return assemblyBytes.Length > 0 ? Load(assemblyBytes) : null;
    }

    public Assembly? Load(byte[] assembly)
    {
        if (assembly.Length == 0)
        {
            DLog.Debug("[ModuleCompiler] 接收到空程序集字节数组, 跳过加载");
            return null;
        }

        var assemblyHash = ComputeSHA256Hash(assembly);
        var shortHash    = FormatHash(assemblyHash);
        var newEntry     = new Lazy<Assembly?>(() => LoadCore(assembly, assemblyHash), LazyThreadSafetyMode.ExecutionAndPublication);
        var cacheEntry   = loadedAssemblyCache.GetOrAdd(assemblyHash, newEntry);

        DLog.Debug
        (
            ReferenceEquals(cacheEntry, newEntry)
                ? $"[ModuleCompiler] 未命中程序集加载缓存, 准备创建新的加载任务, 哈希: {shortHash}, 大小: {assembly.Length} 字节"
                : $"[ModuleCompiler] 命中程序集加载缓存, 哈希: {shortHash}, 大小: {assembly.Length} 字节"
        );

        try
        {
            var loadedAssembly = cacheEntry.Value;

            if (loadedAssembly != null)
            {
                DLog.Debug
                (
                    $"[ModuleCompiler] 程序集加载完成, 哈希: {shortHash}, 程序集: {loadedAssembly.FullName ?? "<未知>"}"
                );

                return loadedAssembly;
            }

            DLog.Warning($"[ModuleCompiler] 程序集加载结果为空, 准备移除缓存项, 哈希: {shortHash}");
        }
        catch (Exception ex)
        {
            DLog.Error($"[ModuleCompiler] 读取程序集加载缓存时发生异常, 哈希: {shortHash}", ex);
        }

        loadedAssemblyCache.TryRemove(assemblyHash, out _);
        DLog.Debug($"[ModuleCompiler] 已移除失效的程序集加载缓存项, 哈希: {shortHash}");
        return null;
    }

    public static IReadOnlyList<Type> GetDerivedTypes<T>(Assembly? assembly) where T : class
    {
        if (assembly == null)
        {
            DLog.Debug($"[ModuleCompiler] 程序集为空, 无法查找派生自 {typeof(T).FullName} 的类型");
            return [];
        }

        var        baseType = typeof(T);
        List<Type> result   = [];

        DLog.Debug
        (
            $"[ModuleCompiler] 开始扫描派生类型, 目标基类: {baseType.FullName}, 程序集: {assembly.FullName ?? "<未知>"}"
        );

        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsAbstract                &&
                    baseType.IsAssignableFrom(type) &&
                    type.GetConstructor(Type.EmptyTypes) != null)
                    result.Add(type);
            }

            DLog.Debug($"[ModuleCompiler] 派生类型扫描完成, 目标基类: {baseType.FullName}, 匹配数量: {result.Count}");
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var type in ex.Types)
            {
                if (type == null                     ||
                    type.IsAbstract                  ||
                    !baseType.IsAssignableFrom(type) ||
                    type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                result.Add(type);
            }

            DLog.Error("[ModuleCompiler] 解析模块类型时发生部分加载失败", ex);
            DLog.Warning
            (
                $"[ModuleCompiler] 已从部分可用类型中恢复结果, 目标基类: {baseType.FullName}, 匹配数量: {result.Count}, 加载器异常数量: {ex.LoaderExceptions.Length}"
            );
        }

        return result;
    }

    private byte[] Compile(string sourceCode)
    {
        var codeHash   = ComputeSHA256Hash(sourceCode);
        var shortHash  = FormatHash(codeHash);
        var newEntry   = new Lazy<byte[]>(() => CompileCore(sourceCode, codeHash), LazyThreadSafetyMode.ExecutionAndPublication);
        var cacheEntry = compiledAssemblyCache.GetOrAdd(codeHash, newEntry);

        DLog.Debug
        (
            ReferenceEquals(cacheEntry, newEntry)
                ? $"[ModuleCompiler] 未命中编译缓存, 开始创建新的编译任务, 源码哈希: {shortHash}, 源码长度: {sourceCode.Length} 字符"
                : $"[ModuleCompiler] 命中编译缓存, 源码哈希: {shortHash}, 源码长度: {sourceCode.Length} 字符"
        );

        try
        {
            var assemblyBytes = cacheEntry.Value;
            DLog.Debug($"[ModuleCompiler] 编译结果已就绪, 源码哈希: {shortHash}, 程序集大小: {assemblyBytes.Length} 字节");
            return assemblyBytes;
        }
        catch (Exception ex)
        {
            compiledAssemblyCache.TryRemove(codeHash, out _);
            DLog.Error($"[ModuleCompiler] 读取编译缓存时发生异常, 已移除缓存项, 源码哈希: {shortHash}", ex);
            throw;
        }
    }

    private byte[] CompileCore(string sourceCode, string codeHash)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var shortHash      = FormatHash(codeHash);

        DLog.Debug($"[ModuleCompiler] 开始执行模块编译, 源码哈希: {shortHash}");

        using var stream = new MemoryStream();

        var emitResult = CreateCompilation(sourceCode, codeHash).Emit(stream);
        if (!emitResult.Success)
            ThrowCompilationException(emitResult, codeHash);

        var assemblyBytes = stream.ToArray();

        DLog.Debug
        (
            $"[ModuleCompiler] 模块编译成功, 源码哈希: {shortHash}, 程序集大小: {assemblyBytes.Length} 字节, 耗时: {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F2} ms"
        );

        return assemblyBytes;
    }

    private CSharpCompilation CreateCompilation(string sourceCode, string codeHash)
    {
        var assemblyName  = $"DailyRoutinesModule-{Guid.NewGuid():N}";
        var syntaxTree    = SyntaxFactory.ParseSyntaxTree(SourceText.From(sourceCode, Encoding.UTF8), ParseOptions);
        var referenceList = ReferenceList;

        DLog.Verbose
        (
            $"[ModuleCompiler] 正在创建 Roslyn 编译实例, 源码哈希: {FormatHash(codeHash)}, 动态程序集名: {assemblyName}, 引用数量: {referenceList.Length}"
        );

        return CSharpCompilation.Create
        (
            assemblyName,
            [syntaxTree],
            referenceList,
            CompilationOptions
        );
    }

    private static void ThrowCompilationException(EmitResult emitResult, string codeHash)
    {
        var diagnostics = emitResult.Diagnostics
                                    .Where
                                    (static diagnostic => diagnostic.IsWarningAsError ||
                                                          diagnostic.Severity == DiagnosticSeverity.Error
                                    )
                                    .ToArray();

        DLog.Error($"[ModuleCompiler] 模块编译失败, 源码哈希: {FormatHash(codeHash)}, 错误数量: {diagnostics.Length}");

        for (var i = 0; i < diagnostics.Length; i++)
            DLog.Error($"[ModuleCompiler] 编译诊断 {i + 1}/{diagnostics.Length}: {FormatDiagnostic(diagnostics[i])}");

        throw new InvalidOperationException($"模块编译失败, 共 {diagnostics.Length} 个错误");
    }

    private Assembly? LoadCore(byte[] assembly, string assemblyHash)
    {
        var startTimestamp    = Stopwatch.GetTimestamp();
        var parentLoadContext = AssemblyLoadContext.GetLoadContext(typeof(ModuleCompiler).Assembly);
        var contextName       = $"DailyRoutinesModule-{Guid.NewGuid():N}";

        if (parentLoadContext == null)
        {
            DLog.Error($"[ModuleCompiler] 无法获取父程序集加载上下文, 程序集哈希: {FormatHash(assemblyHash)}");
            return null;
        }

        using var stream      = new MemoryStream(assembly, false);
        var       loadContext = new ModuleLoadContext(parentLoadContext, contextName);

        DLog.Debug
        (
            $"[ModuleCompiler] 开始加载动态程序集, 哈希: {FormatHash(assemblyHash)}, 上下文: {contextName}, 大小: {assembly.Length} 字节"
        );

        try
        {
            var loadedAssembly = loadContext.LoadFromStream(stream);
            loadedContexts.Add(loadContext);
            DLog.Debug
            (
                $"[ModuleCompiler] 动态程序集加载成功, 哈希: {FormatHash(assemblyHash)}, 程序集: {loadedAssembly.FullName ?? "<未知>"}, 上下文: {contextName}, 耗时: {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F2} ms"
            );

            return loadedAssembly;
        }
        catch (Exception ex)
        {
            TryUnloadContext(loadContext);
            DLog.Error
            (
                $"[ModuleCompiler] 加载动态程序集时发生异常, 哈希: {FormatHash(assemblyHash)}, 上下文: {contextName}",
                ex
            );
            return null;
        }
    }

    private static ImmutableArray<MetadataReference> BuildReferenceList()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var builder        = ImmutableArray.CreateBuilder<MetadataReference>();
        var seenFileNames  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        DLog.Debug("[ModuleCompiler] 开始构建 Roslyn 引用列表");

        AddReferences(builder, seenFileNames, Path.GetDirectoryName(typeof(object).Assembly.Location), "系统基础程序集目录",           SearchOption.TopDirectoryOnly);
        AddReferences(builder, seenFileNames, Path.GetDirectoryName(typeof(Form).Assembly.Location),   "Windows Forms 程序集目录", SearchOption.TopDirectoryOnly);
        AddReferences(builder, seenFileNames, IDalamudPluginInterface.Instance().AssemblyLocation.DirectoryName,   "插件程序集目录",             SearchOption.AllDirectories);
        AddReferences
        (
            builder,
            seenFileNames,
            Path.GetDirectoryName(IDalamudPluginInterface.Instance().GetType().Assembly.Location),
            "插件运行时类型目录",
            SearchOption.AllDirectories
        );

        DLog.Debug
        (
            $"[ModuleCompiler] Roslyn 引用列表构建完成, 引用数量: {builder.Count}, 耗时: {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:F2} ms"
        );

        return builder.ToImmutable();
    }

    private static void AddReferences
    (
        ImmutableArray<MetadataReference>.Builder builder,
        HashSet<string>                           seenFileNames,
        string?                                   directory,
        string                                    sourceName,
        SearchOption                              searchOption
    )
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            DLog.Warning($"[ModuleCompiler] 引用目录不可用, 来源: {sourceName}, 目录: {directory ?? "<空>"}");
            return;
        }

        var discoveredCount = 0;
        var duplicateCount  = 0;
        var invalidCount    = 0;
        var addedCount      = 0;

        foreach (var path in EnumerateAssemblyFiles(directory, searchOption))
        {
            discoveredCount++;

            var fileName = Path.GetFileName(path);

            if (!seenFileNames.Add(fileName))
            {
                duplicateCount++;
                continue;
            }

            if (!IsValidAssembly(path))
            {
                invalidCount++;
                continue;
            }

            builder.Add(MetadataReference.CreateFromFile(path));
            addedCount++;
        }

        DLog.Debug
        (
            $"[ModuleCompiler] 引用目录扫描完成, 来源: {sourceName}, 目录: {directory}, 发现文件: {discoveredCount}, 新增引用: {addedCount}, 重复文件名: {duplicateCount}, 无效程序集: {invalidCount}"
        );
    }

    private static IEnumerable<string> EnumerateAssemblyFiles(string directory, SearchOption searchOption)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*.dll", searchOption))
            yield return path;

        foreach (var path in Directory.EnumerateFiles(directory, "*.exe", searchOption))
            yield return path;
    }

    private static bool IsValidAssembly(string path)
    {
        try
        {
            _ = AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ComputeSHA256Hash(string data) =>
        ComputeSHA256Hash(Encoding.UTF8.GetBytes(data));

    private static string ComputeSHA256Hash(byte[] data) =>
        ComputeSHA256Hash(data.AsSpan());

    private static string ComputeSHA256Hash(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(SHA256.HashData(data));

    private static string FormatHash(string hash) =>
        hash.Length <= 12 ? hash : hash[..12];

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        if (!lineSpan.IsValid)
            return $"{diagnostic.Id}, 消息: {diagnostic.GetMessage()}";

        var line   = lineSpan.StartLinePosition.Line      + 1;
        var column = lineSpan.StartLinePosition.Character + 1;

        return $"{diagnostic.Id}, 位置: 第 {line} 行, 第 {column} 列, 消息: {diagnostic.GetMessage()}";
    }

    private static void TryUnloadContext(ModuleLoadContext context)
    {
        var contextName = context.Name ?? "<未命名>";
        DLog.Verbose($"[ModuleCompiler] 开始卸载动态程序集上下文: {contextName}");

        try
        {
            context.Unload();
            DLog.Verbose($"[ModuleCompiler] 已发出动态程序集上下文卸载请求: {contextName}");
        }
        catch (Exception ex)
        {
            DLog.Error($"[ModuleCompiler] 卸载动态程序集上下文时发生异常, 上下文: {contextName}", ex);
        }
    }
}
