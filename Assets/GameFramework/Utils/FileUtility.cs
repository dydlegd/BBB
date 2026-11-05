using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 通用文件操作工具类。
/// 提供文件读写、删除、复制、移动、目录操作、路径处理等常用功能。
/// 支持断电安全的原子写入（临时文件 + File.Replace）和异步操作。
/// </summary>
/// <example>基本读写
/// <code>
/// string dir = Application.persistentDataPath;
/// string path = Path.Combine(dir, "config.json");
///
/// // 文本写入
/// FileUtility.WriteAllText(path, "{\"volume\": 0.8}");
/// FileUtility.WriteAllText(path, "\n更多内容", append: true);
///
/// // 行数组写入
/// FileUtility.WriteAllLines(path, new[] { "第1行", "第2行" });
///
/// // 字节写入（注意用 Path.Combine，不要写 path + "/icon.png"）
/// byte[] png = screenshot.EncodeToPNG();
/// FileUtility.WriteAllBytes(Path.Combine(dir, "icon.png"), png);
///
/// // 读取
/// string json = FileUtility.ReadAllText(path);
/// string[] lines = FileUtility.ReadAllLines(path);
/// byte[] data = FileUtility.ReadAllBytes(path);
/// </code>
/// </example>
/// <example>原子写入（断电安全）
/// <code>
/// string savePath = Path.Combine(Application.persistentDataPath, "save.dat");
///
/// // 写入：先写 .tmp → 旧文件变 .bak → .tmp 原子替换为主文件
/// // 进程崩溃/被杀时，主文件要么完整旧值、要么完整新值
/// FileUtility.WriteAllTextAtomic(savePath, gameStateJson);
/// FileUtility.WriteAllBytesAtomic(savePath, encryptedData);
///
/// // 读取：按 主文件 -> .bak 旧版本 -> .tmp 残留 的顺序回滚
/// // .bak 是完整旧版本，优先于可能半写的 .tmp
/// string safeText = FileUtility.ReadAllTextSafe(savePath);
/// byte[] safeBytes = FileUtility.ReadAllBytesSafe(savePath);
///
/// // 关键：传入内容校验，才能发现"能打开但被截断"的残缺文件
/// // 不传 validate 时，残缺文件会被当成读取成功，.bak 永远不会回滚
/// string json = FileUtility.ReadAllTextSafe(savePath, validate: text => IsValidJson(text));
/// </code>
/// </example>
/// <example>异步操作（不阻塞UI）
/// <code>
/// string text = await FileUtility.ReadAllTextAsync(path);
/// byte[] bytes = await FileUtility.ReadAllBytesAsync(path);
/// string safeText = await FileUtility.ReadAllTextSafeAsync(savePath);
/// string[] safeLines = await FileUtility.ReadAllLinesSafeAsync(savePath, validate: lines => lines.Length > 0);
///
/// await FileUtility.WriteAllTextAsync(path, largeJson);
/// await FileUtility.WriteAllLinesAsync(path, lines);
/// await FileUtility.WriteAllBytesAtomicAsync(savePath, encryptedData);
/// await FileUtility.WriteAllLinesAtomicAsync(savePath, lines);
/// </code>
/// </example>
/// <example>文件操作
/// <code>
/// if (FileUtility.FileExists(path)) { ... }
///
/// long size = FileUtility.GetFileSize(path);               // -1=不存在
/// string md5 = FileUtility.GetFileMD5(path);               // MD5校验（同步，大文件阻塞）
/// string md5Async = await FileUtility.GetFileMD5Async(path); // 后台线程计算
/// DateTime t = FileUtility.GetLastWriteTime(path);         // 最后修改时间
///
/// FileUtility.DeleteFile(path);
/// FileUtility.CopyFile(srcPath, dstPath, overwrite: true);
/// FileUtility.MoveFile(oldPath, newPath);                     // 目标已存在时返回 false
/// FileUtility.MoveFile(oldPath, newPath, overwrite: true);    // 显式覆盖
/// FileUtility.RenameFile(path, "newName.json");
/// </code>
/// </example>
/// <example>目录操作
/// <code>
/// if (FileUtility.DirectoryExists(dir)) { ... }
///
/// FileUtility.CreateDirectory(dir);
/// FileUtility.DeleteDirectory(dir, recursive: true);
///
/// string[] allTxt = FileUtility.GetFiles(dir, "*.txt", SearchOption.AllDirectories);
/// string[] subDirs = FileUtility.GetDirectories(dir);
/// </code>
/// </example>
/// <example>路径处理
/// <code>
/// string path = FileUtility.CombinePaths("C:\\", "Data", "save.json"); // C:\Data\save.json
/// string ext = FileUtility.GetExtension("data.json");                  // ".json"
/// string name = FileUtility.GetFileName("C:/Data/a.txt");              // "a.txt"
/// string dir = FileUtility.GetDirectoryPath("C:/Data/a.txt");          // "C:\Data"
/// string abs = FileUtility.ToAbsolutePath("Assets/Config.json");
/// </code>
/// </example>
/// <example>文件搜索
/// <code>
/// List<string> files = FileUtility.SearchFilesByName(dir, "config");
/// List<string> matched = FileUtility.SearchFilesByContent(dir, "TODO", "*.cs");
/// </code>
/// </example>
public static class FileUtility
{
    private const string TmpSuffix = ".tmp";
    private const string BakSuffix = ".bak";
    /// <summary>CopyFileAsync 的临时文件后缀（不是 .tmp：拷贝的中间文件不参与断电回滚）</summary>
    private const string PartialSuffix = ".partial.";

    /// <summary>
    /// 默认编码：UTF-8 且不写 BOM。
    /// <para>全类所有「未显式指定编码」的读写都用它，保证普通写入与原子写入产出的字节完全一致，
    /// BOM 有无不再取决于走哪条代码路径。</para>
    /// <para>调用方显式传入 <see cref="Encoding.UTF8"/>（带 BOM）时，原子写入也会同样补上 BOM。</para>
    /// </summary>
    private static readonly Encoding DefaultEncoding = new UTF8Encoding(false);

    #region 同步读取

    /// <summary>
    /// 读取文本文件内容。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>文件内容，读取失败返回null</returns>
    public static string ReadAllText(string filePath, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllText: 文件路径为空");
            return null;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[FileUtility] ReadAllText: 文件不存在 [{filePath}]");
                return null;
            }

            encoding = encoding ?? DefaultEncoding;
            return File.ReadAllText(filePath, encoding);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] ReadAllText 失败 [{filePath}]: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 读取文本文件的所有行。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>文件所有行，读取失败返回null</returns>
    public static string[] ReadAllLines(string filePath, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllLines: 文件路径为空");
            return null;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[FileUtility] ReadAllLines: 文件不存在 [{filePath}]");
                return null;
            }

            encoding = encoding ?? DefaultEncoding;
            return File.ReadAllLines(filePath, encoding);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] ReadAllLines 失败 [{filePath}]: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 读取文件的所有字节。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件字节数组，读取失败返回null</returns>
    public static byte[] ReadAllBytes(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllBytes: 文件路径为空");
            return null;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[FileUtility] ReadAllBytes: 文件不存在 [{filePath}]");
                return null;
            }

            return File.ReadAllBytes(filePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] ReadAllBytes 失败 [{filePath}]: {e.Message}");
            return null;
        }
    }

    #endregion

    #region 断电安全读取（带备份回滚）

    /// <summary>候选文件的读取结果：把「文件不存在」和「存在但读不出来」区分开</summary>
    private enum ReadOutcome
    {
        /// <summary>文件不存在（或路径为空），调用方可以继续沿回滚链尝试下一个候选</summary>
        Missing,
        /// <summary>文件存在但读取失败（占用 / 无权限 / IO 故障），属于环境问题，不能当成内容损坏</summary>
        Failed,
        /// <summary>读取成功</summary>
        Ok
    }

    /// <summary>
    /// 断电安全读取文本：按 主文件 -> .bak 备份 -> .tmp 残留 的顺序尝试读取。
    /// <para>主文件损坏（截断 / 校验不通过）时优先回滚到完整的 .bak 上一版本；
    /// .bak 也不可用时才尝试 .tmp 残留，仅用于覆盖「首次写入后断电」这类本来就没有备份的场景。</para>
    /// <para>.tmp 可能只写了一半，可靠性低于 .bak，因此排在最后。</para>
    /// <para>只有「文件不存在」和「内容校验不通过」才会继续往下回滚；主文件存在但读不出来
    /// （被其它进程占用、无权限等）时直接返回 null 并打错误日志，绝不静默返回旧备份——
    /// 否则一次瞬时占用就会让调用方拿到过期数据，并在下次存盘时把新数据覆盖掉。</para>
    /// <para>注意：不传 <paramref name="validate"/> 时只能发现「打不开」的损坏，发现不了「能读但内容残缺」，
    /// 而断电最常见的正是后者。存档类调用务必传校验委托。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码格式，默认UTF-8（无BOM）</param>
    /// <param name="validate">内容校验委托，返回 false 视为该副本损坏并继续尝试下一个</param>
    /// <returns>文件内容，全部失败返回null</returns>
    public static string ReadAllTextSafe(string filePath, Encoding encoding = null, Func<string, bool> validate = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllTextSafe: 文件路径为空");
            return null;
        }

        encoding = encoding ?? DefaultEncoding;

        string content;
        ReadOutcome outcome = TryReadExisting(filePath, path => File.ReadAllText(path, encoding), out content);
        if (outcome == ReadOutcome.Failed)
            return null;
        if (outcome == ReadOutcome.Ok && IsValid(content, validate))
            return content;

        string bak = filePath + BakSuffix;
        string bakContent;
        outcome = TryReadExisting(bak, path => File.ReadAllText(path, encoding), out bakContent);
        if (outcome == ReadOutcome.Failed)
            return null;
        if (outcome == ReadOutcome.Ok && IsValid(bakContent, validate))
        {
            Debug.LogWarning($"[FileUtility] 主文件损坏，已从备份恢复 [{Path.GetFileName(filePath)}]");
            // 回滚后必须修复主文件：否则下一次原子写会把"损坏的主文件"轮换成新的 .bak，覆盖唯一的好备份
            RepairMainFileWithBytes(filePath, EncodeWithPreamble(encoding, bakContent));
            return bakContent;
        }

        return TryRecoverFromTmp(filePath, path => File.ReadAllText(path, encoding), validate);
    }

    /// <summary>
    /// 断电安全读取多行文本：按 主文件 -> .bak 备份 -> .tmp 残留 的顺序尝试读取。
    /// <para>行为与 <see cref="ReadAllTextSafe"/> 一致：主文件存在但读不出来时直接失败，不回滚到旧备份。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码格式，默认UTF-8（无BOM）</param>
    /// <param name="validate">内容校验委托，返回 false 视为该副本损坏并继续尝试下一个</param>
    /// <returns>文件各行内容，全部失败返回null</returns>
    public static string[] ReadAllLinesSafe(string filePath, Encoding encoding = null, Func<string[], bool> validate = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllLinesSafe: 文件路径为空");
            return null;
        }

        encoding = encoding ?? DefaultEncoding;

        string[] lines;
        ReadOutcome outcome = TryReadExisting(filePath, path => File.ReadAllLines(path, encoding), out lines);
        if (outcome == ReadOutcome.Failed)
            return null;
        if (outcome == ReadOutcome.Ok && IsValid(lines, validate))
            return lines;

        string bak = filePath + BakSuffix;
        string[] bakLines;
        outcome = TryReadExisting(bak, path => File.ReadAllLines(path, encoding), out bakLines);
        if (outcome == ReadOutcome.Failed)
            return null;
        if (outcome == ReadOutcome.Ok && IsValid(bakLines, validate))
        {
            Debug.LogWarning($"[FileUtility] 主文件损坏，已从备份恢复 [{Path.GetFileName(filePath)}]");
            RepairMainFileWithBytes(filePath, EncodeWithPreamble(encoding, JoinLines(bakLines)));
            return bakLines;
        }

        return TryRecoverFromTmp(filePath, path => File.ReadAllLines(path, encoding), validate);
    }

    /// <summary>
    /// 断电安全读取字节：按 主文件 -> .bak 备份 -> .tmp 残留 的顺序尝试读取。
    /// <para>行为与 <see cref="ReadAllTextSafe"/> 一致：主文件存在但读不出来时直接失败，不回滚到旧备份。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="validate">内容校验委托，返回 false 视为该副本损坏并继续尝试下一个</param>
    /// <returns>文件字节内容，全部失败返回null</returns>
    public static byte[] ReadAllBytesSafe(string filePath, Func<byte[], bool> validate = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllBytesSafe: 文件路径为空");
            return null;
        }

        byte[] data;
        ReadOutcome outcome = TryReadExisting(filePath, File.ReadAllBytes, out data);
        if (outcome == ReadOutcome.Failed)
            return null;
        if (outcome == ReadOutcome.Ok && IsValid(data, validate))
            return data;

        string bak = filePath + BakSuffix;
        byte[] bakData;
        outcome = TryReadExisting(bak, File.ReadAllBytes, out bakData);
        if (outcome == ReadOutcome.Failed)
            return null;
        if (outcome == ReadOutcome.Ok && IsValid(bakData, validate))
        {
            Debug.LogWarning($"[FileUtility] 主文件损坏，已从备份恢复 [{Path.GetFileName(filePath)}]");
            RepairMainFileWithBytes(filePath, bakData);
            return bakData;
        }

        return TryRecoverFromTmp(filePath, File.ReadAllBytes, validate);
    }

    /// <summary>
    /// 尝试从残留 .tmp 恢复内容并写回主文件。
    /// <para>读取与安装都在路径锁内完成，避免读到另一个线程正在原子写入的半截 .tmp。</para>
    /// <para>内容校验在锁外执行：校验委托可能很重（解密 / JSON 解析 / 甚至访问文件），
    /// 放在锁内会把同路径的原子写全部阻塞，也可能因重入导致死锁。</para>
    /// <para>多个残留按最后写入时间从新到旧依次校验，跳过损坏的那一个而不是直接放弃。</para>
    /// <para>安装用原地替换，绝不「先删主文件再移动」——否则移动失败会留下主文件缺失的状态。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="reader">读取委托</param>
    /// <param name="validate">内容校验委托</param>
    /// <returns>恢复出的内容，全部不可用返回null</returns>
    private static T TryRecoverFromTmp<T>(string filePath, Func<string, T> reader, Func<T, bool> validate = null) where T : class
    {
        string[] tmpFiles = GetTmpFilesNewestFirst(filePath);
        if (tmpFiles == null || tmpFiles.Length == 0)
            return null;

        T recovered = null;
        string tmpFile = null;

        // 从新到旧逐个校验：最新的那个可能正是崩溃时写了一半的残件
        for (int i = 0; i < tmpFiles.Length; i++)
        {
            T candidate;
            lock (GetPathLock(filePath))
            {
                candidate = ReadWithCandidate(tmpFiles[i], reader);
            }

            // 校验放在锁外，避免长时间占用同路径的写入锁
            if (!IsValid(candidate, validate))
                continue;

            recovered = candidate;
            tmpFile = tmpFiles[i];
            break;
        }

        if (tmpFile == null)
            return null;

        lock (GetPathLock(filePath))
        {
            try
            {
                // 主文件此时是缺失或损坏的：原地替换保证「要么完整新值、要么原样不动」
                if (File.Exists(filePath))
                    SwapInPlace(tmpFile, filePath);
                else
                    File.Move(tmpFile, filePath);

                Debug.LogWarning($"[FileUtility] 从.tmp残留恢复 [{Path.GetFileName(filePath)}]");
                CleanupTmpFiles(filePath);
            }
            catch (Exception e)
            {
                // 安装失败时保留 .tmp（它是唯一可用数据），只记录日志
                Debug.LogWarning($"[FileUtility] .tmp恢复移动失败: {e.Message}");
            }
        }

        return recovered;
    }

    /// <summary>
    /// 从候选路径读取，任何异常都返回 default。只用于 .tmp 残留这种「读不出来就跳过下一个」的场景，
    /// 主文件 / .bak 的读取必须走 <see cref="TryReadExisting"/>，不能把读失败吞掉。
    /// </summary>
    /// <param name="path">候选文件路径</param>
    /// <param name="reader">读取委托</param>
    /// <returns>读取结果，失败返回 default</returns>
    private static T ReadWithCandidate<T>(string path, Func<string, T> reader)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return default;

        try
        {
            return reader(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FileUtility] 读取候选文件失败 [{Path.GetFileName(path)}]: {e.Message}");
        }

        return default;
    }

    /// <summary>
    /// 读取候选文件，并把「不存在」与「读失败」区分开。
    /// <para>不存在返回 <see cref="ReadOutcome.Missing"/>（value 为 default），调用方继续走回滚链；
    /// 读失败返回 <see cref="ReadOutcome.Failed"/>，调用方必须直接失败而不是回滚，
    /// 避免把瞬时占用 / 权限问题误判成内容损坏并静默返回旧备份。</para>
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="reader">读取委托</param>
    /// <param name="value">读到的内容</param>
    /// <returns>读取结果</returns>
    private static ReadOutcome TryReadExisting<T>(string path, Func<string, T> reader, out T value)
    {
        value = default;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return ReadOutcome.Missing;

        try
        {
            value = reader(path);
            return ReadOutcome.Ok;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] 读取失败（按环境故障处理，不回退备份） [{Path.GetFileName(path)}]: {e.Message}");
            return ReadOutcome.Failed;
        }
    }

    /// <summary>
    /// 用备份内容原地修复主文件（不产出新的 .bak）。
    /// <para>为什么必须修复：主文件损坏时若只返回备份内容而不修主文件，下一次原子写会把「损坏的主文件」
    /// 轮换成新的 .bak，把唯一可用的好备份覆盖掉，此后就再无回滚点。</para>
    /// <para>修复失败只记录警告，不影响本次读取结果。</para>
    /// </summary>
    /// <param name="filePath">主文件路径</param>
    /// <param name="data">用来修复的完整内容</param>
    private static void RepairMainFileWithBytes(string filePath, byte[] data)
    {
        if (data == null)
            return;

        lock (GetPathLock(filePath))
        {
            string tmp = filePath + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + TmpSuffix;
            try
            {
                WriteDurable(tmp, data);
                if (File.Exists(filePath))
                    SwapInPlace(tmp, filePath);   // 不传备份目标：保住现有的好备份
                else
                    File.Move(tmp, filePath);

                Debug.LogWarning($"[FileUtility] 已用备份内容修复主文件 [{Path.GetFileName(filePath)}]");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FileUtility] 备份回滚后修复主文件失败 [{filePath}]: {e.Message}");
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch (Exception cleanupEx)
                {
                    Debug.LogWarning($"[FileUtility] 清理修复临时文件失败 [{tmp}]: {cleanupEx.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 按 File.WriteAllLines 的规则把行数组拼回文本：每行末尾都带换行符（末行也带），空数组得空串。
    /// </summary>
    /// <param name="lines">行数组</param>
    /// <returns>拼好的文本</returns>
    private static string JoinLines(string[] lines)
    {
        if (lines == null || lines.Length == 0)
            return string.Empty;

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    /// <summary>
    /// 枚举文件对应的残留 .tmp，按最后写入时间从新到旧排序（时间读不到的排在最后）。
    /// </summary>
    /// <param name="filePath">主文件路径</param>
    /// <returns>按时间从新到旧的 .tmp 路径数组</returns>
    private static string[] GetTmpFilesNewestFirst(string filePath)
    {
        string[] tmpFiles = GetTmpFiles(filePath);
        if (tmpFiles == null || tmpFiles.Length <= 1)
            return tmpFiles;

        var stamps = new List<KeyValuePair<string, DateTime>>(tmpFiles.Length);
        foreach (string tmp in tmpFiles)
        {
            DateTime writeTime = DateTime.MinValue;
            try
            {
                writeTime = File.GetLastWriteTimeUtc(tmp);
            }
            catch (Exception)
            {
                // 枚举之后文件被删除/占用，按最旧处理即可
            }
            stamps.Add(new KeyValuePair<string, DateTime>(tmp, writeTime));
        }

        stamps.Sort((a, b) => b.Value.CompareTo(a.Value));

        string[] ordered = new string[stamps.Count];
        for (int i = 0; i < ordered.Length; i++)
            ordered[i] = stamps[i].Key;
        return ordered;
    }

    /// <summary>
    /// 获取指定文件对应的所有残留 .tmp（按「主文件名.*.tmp」命名约定枚举）。
    /// </summary>
    /// <param name="filePath">主文件路径</param>
    /// <returns>残留 .tmp 路径数组：确实没有残留返回空数组；目录无法枚举时返回 null，
    /// 调用方据此区分「确实没有」与「无法确认」</returns>
    private static string[] GetTmpFiles(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return new string[0];

        string directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(directory))
            directory = ".";
        if (string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
            return new string[0];

        try
        {
            return Directory.GetFiles(directory, fileName + ".*" + TmpSuffix);
        }
        catch (Exception e)
        {
            // 枚举失败时返回 null：调用方不能把「读不了目录」当成「没有残留」
            Debug.LogWarning($"[FileUtility] 枚举临时文件失败 [{directory}]: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 删除指定文件的所有残留 .tmp。只应在持有该路径的锁、或确认没有并发写入时调用。
    /// </summary>
    /// <param name="filePath">主文件路径</param>
    private static void CleanupTmpFiles(string filePath)
    {
        string[] tmpFiles = GetTmpFiles(filePath);
        if (tmpFiles == null || tmpFiles.Length == 0)
            return;

        foreach (string tmp in tmpFiles)
        {
            try
            {
                DeleteFile(tmp);
            }
            catch (Exception)
            {
                // 文件不存在 / 被占用，跳过即可
            }
        }
    }

    /// <summary>
    /// 清理超过 minAge 的遗留临时文件：崩溃遗留的 .tmp 与拷贝遗留的 .partial 副本。
    /// 只删「够老」的，避免误伤正在并发写入的临时文件。
    /// </summary>
    /// <param name="filePath">主文件路径</param>
    /// <param name="minAge">最小存活时间</param>
    private static void CleanupStaleTmpFiles(string filePath, TimeSpan minAge)
    {
        string[] tmpFiles = GetTmpFiles(filePath);
        if (tmpFiles != null && tmpFiles.Length > 0)
        {
            DateTime threshold = DateTime.UtcNow - minAge;
            foreach (string tmp in tmpFiles)
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(tmp) < threshold)
                        DeleteFile(tmp);
                }
                catch (Exception)
                {
                    // 枚举之后文件被删除/占用，跳过即可
                }
            }
        }

        CleanupStalePartialFiles(filePath, minAge);
    }

    /// <summary>
    /// 清理 CopyFileAsync 遗留的 .partial 临时文件：它们不在 .tmp 命名空间里，
    /// 不单独扫一遍的话，拷贝途中崩溃就会永久残留。
    /// </summary>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="minAge">最小存活时间</param>
    private static void CleanupStalePartialFiles(string filePath, TimeSpan minAge)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        string directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(directory))
            directory = ".";
        if (string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
            return;

        try
        {
            DateTime threshold = DateTime.UtcNow - minAge;
            foreach (string partial in Directory.GetFiles(directory, fileName + PartialSuffix + "*"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(partial) < threshold)
                        DeleteFile(partial);
                }
                catch (Exception)
                {
                    // 枚举之后文件被删除/占用，跳过即可
                }
            }
        }
        catch (Exception)
        {
            // 目录不可枚举，忽略
        }
    }

    /// <summary>按目录记录上次清理时间，避免每次写入都扫一遍目录</summary>
    private static readonly Dictionary<string, DateTime> s_lastStaleTmpCleanupUtc =
        new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_staleTmpCleanupLock = new object();
    /// <summary>同一目录两次清理之间的最小间隔</summary>
    private static readonly TimeSpan StaleTmpCleanupInterval = TimeSpan.FromSeconds(10);
    /// <summary>小于该年龄的临时文件一律不删（可能是并发写入正在用的）</summary>
    private static readonly TimeSpan StaleTmpMinAge = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 带节流的残留临时文件清理：同一目录每 10 秒最多清理一次。
    /// </summary>
    /// <param name="filePath">主文件路径</param>
    private static void TryCleanupStaleTmpFiles(string filePath)
    {
        string directory;
        try
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
        }
        catch (Exception)
        {
            return;
        }

        lock (s_staleTmpCleanupLock)
        {
            if (s_lastStaleTmpCleanupUtc.TryGetValue(directory, out DateTime last)
                && DateTime.UtcNow - last < StaleTmpCleanupInterval)
                return;

            s_lastStaleTmpCleanupUtc[directory] = DateTime.UtcNow;
        }

        CleanupStaleTmpFiles(filePath, StaleTmpMinAge);
    }

    /// <summary>
    /// 内容校验：null 一律视为损坏（读失败不应该被当成「空内容」放行）；
    /// 没传校验委托时只按「能读出来」判断；校验委托自身抛异常时按「校验不通过」处理并记录错误，
    /// 绝不让异常从本类的公共读取 API 逃出去。
    /// </summary>
    /// <param name="value">待校验内容</param>
    /// <param name="validate">校验委托</param>
    /// <returns>有效返回 true</returns>
    private static bool IsValid<T>(T value, Func<T, bool> validate) where T : class
    {
        if (ReferenceEquals(value, null))
            return false;

        if (validate == null)
            return true;

        try
        {
            return validate(value);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] 内容校验委托抛异常，按校验不通过处理: {e}");
            return false;
        }
    }

    #endregion

    #region 路径锁（同路径写入串行化）

    /// <summary>
    /// 路径锁表：同一路径的原子写入与 .tmp 恢复必须串行执行，
    /// 否则 File.Replace 相互冲突、.bak 会被交错覆盖。
    /// </summary>
    private static readonly Dictionary<string, object> s_pathLocks = new Dictionary<string, object>();

    /// <summary>
    /// 是否大小写不敏感的文件系统（Windows 与 macOS 默认如此）。
    /// 这类系统上 "Data.json" 和 "data.json" 是同一个文件，锁键必须统一大小写，否则会拿到两把锁、锁形同虚设。
    /// </summary>
    private static readonly bool s_caseInsensitiveFileSystem =
        Path.DirectorySeparatorChar == '\\'
        || Application.platform == RuntimePlatform.OSXEditor
        || Application.platform == RuntimePlatform.OSXPlayer;

    /// <summary>
    /// 获取指定文件路径对应的锁对象（按规范化绝对路径为键）。
    /// <para>锁对象常驻内存不回收，数量等于被写入过的文件数，可接受。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>该路径对应的锁对象</returns>
    private static object GetPathLock(string filePath)
    {
        string key = GetPathLockKey(filePath);

        lock (s_pathLocks)
        {
            if (!s_pathLocks.TryGetValue(key, out object gate))
            {
                gate = new object();
                s_pathLocks[key] = gate;
            }
            return gate;
        }
    }

    /// <summary>
    /// 把路径规范化为锁键：绝对化，并在大小写不敏感的文件系统上统一大写。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>锁键</returns>
    private static string GetPathLockKey(string filePath)
    {
        string key = filePath ?? string.Empty;
        try
        {
            key = Path.GetFullPath(key);
        }
        catch (Exception)
        {
            // 非法路径无法规范化，退化为使用原始字符串作为键
        }

        return s_caseInsensitiveFileSystem ? key.ToUpperInvariant() : key;
    }

    /// <summary>
    /// 同时持有两个路径的锁并执行 <paramref name="action"/>。
    /// <para>按锁键的固定字典序加锁，避免两个线程交叉移动（A→B 与 B→A）时互相等待形成死锁。</para>
    /// <para>两个路径规范化后相同（同一个文件）时只加一把锁。</para>
    /// </summary>
    /// <param name="firstPath">第一个路径</param>
    /// <param name="secondPath">第二个路径</param>
    /// <param name="action">持锁期间要执行的操作</param>
    private static void WithPathLocks(string firstPath, string secondPath, Action action)
    {
        string firstKey = GetPathLockKey(firstPath);
        string secondKey = GetPathLockKey(secondPath);

        if (firstKey == secondKey)
        {
            lock (GetPathLock(firstPath))
            {
                action();
            }
            return;
        }

        if (string.CompareOrdinal(firstKey, secondKey) <= 0)
        {
            lock (GetPathLock(firstPath))
            {
                lock (GetPathLock(secondPath))
                {
                    action();
                }
            }
            return;
        }

        lock (GetPathLock(secondPath))
        {
            lock (GetPathLock(firstPath))
            {
                action();
            }
        }
    }

    #endregion

    #region 同步写入

    /// <summary>
    /// 写入文本到文件（普通模式，非原子）。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <param name="append">是否追加模式，默认false（覆盖）</param>
    /// <returns>是否写入成功</returns>
    public static bool WriteAllText(string filePath, string content, Encoding encoding = null, bool append = false)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllText: 文件路径为空");
            return false;
        }

        if (content == null)
        {
            Debug.LogError("[FileUtility] WriteAllText: 内容为空");
            return false;
        }

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(filePath));
            encoding = encoding ?? DefaultEncoding;

            if (append)
            {
                File.AppendAllText(filePath, content, encoding);
            }
            else
            {
                File.WriteAllText(filePath, content, encoding);
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllText 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 写入行数组到文件（普通模式，非原子）。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="lines">要写入的行数组</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>是否写入成功</returns>
    public static bool WriteAllLines(string filePath, string[] lines, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllLines: 文件路径为空");
            return false;
        }

        if (lines == null)
        {
            Debug.LogError("[FileUtility] WriteAllLines: 内容数组为空");
            return false;
        }

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(filePath));
            encoding = encoding ?? DefaultEncoding;
            File.WriteAllLines(filePath, lines, encoding);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllLines 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 写入字节数组到文件（普通模式，非原子）。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要写入的字节数组</param>
    /// <returns>是否写入成功</returns>
    public static bool WriteAllBytes(string filePath, byte[] data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllBytes: 文件路径为空");
            return false;
        }

        if (data == null)
        {
            Debug.LogError("[FileUtility] WriteAllBytes: 数据为空");
            return false;
        }

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(filePath));
            File.WriteAllBytes(filePath, data);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllBytes 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    #endregion

    #region 断电安全写入（原子写盘）
#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>POSIX rename，原子覆盖；SetLastError 用于失败时读取 errno</summary>
    [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
    private static extern int rename(string oldpath, string newpath);
#endif

    /// <summary>
    /// 原子写入文本：先写 .tmp，再 File.Replace 原子改名。
    /// 旧主文件 -> .bak 备份，.tmp -> 主文件。
    /// 进程崩溃/被杀时，主文件要么是完整旧值、要么是完整新值，不会出现半截文件。
    /// 注意：改名后不做目录 fsync，真正的掉电仍可能丢失 rename 的目录项，此时会回滚到 .bak。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>是否写入成功</returns>
    public static bool WriteAllTextAtomic(string filePath, string content, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllTextAtomic: 文件路径为空");
            return false;
        }

        if (content == null)
        {
            Debug.LogError("[FileUtility] WriteAllTextAtomic: 内容为空");
            return false;
        }

        try
        {
            encoding = encoding ?? DefaultEncoding;
            byte[] data = EncodeWithPreamble(encoding, content);
            return AtomicWrite(filePath, data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllTextAtomic 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 原子写入行数组：先写 .tmp，再 File.Replace 原子改名。
    /// <para>与 File.WriteAllLines 保持一致，每行末尾都带换行符（末行也带），
    /// 避免与普通写法的文件内容出现假差异。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="lines">要写入的行数组</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>是否写入成功</returns>
    public static bool WriteAllLinesAtomic(string filePath, string[] lines, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllLinesAtomic: 文件路径为空");
            return false;
        }

        if (lines == null)
        {
            Debug.LogError("[FileUtility] WriteAllLinesAtomic: 内容数组为空");
            return false;
        }

        try
        {
            encoding = encoding ?? DefaultEncoding;

            // 空数组时 File.WriteAllLines 会创建空文件，这里保持一致
            string content = lines.Length > 0
                ? string.Join(Environment.NewLine, lines) + Environment.NewLine
                : string.Empty;

            byte[] data = EncodeWithPreamble(encoding, content);
            return AtomicWrite(filePath, data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllLinesAtomic 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 原子写入字节数组：先写 .tmp，再 File.Replace 原子改名。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要写入的字节数组</param>
    /// <returns>是否写入成功</returns>
    public static bool WriteAllBytesAtomic(string filePath, byte[] data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllBytesAtomic: 文件路径为空");
            return false;
        }

        if (data == null)
        {
            Debug.LogError("[FileUtility] WriteAllBytesAtomic: 数据为空");
            return false;
        }

        try
        {
            return AtomicWrite(filePath, data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllBytesAtomic 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 按编码把字符串编码为字节，并在编码器声明了 BOM 时补上 BOM。
    /// <para>这样原子写入与 <see cref="File.WriteAllText"/> 产出的字节完全一致——
    /// 无论调用方传的是默认编码还是显式带 BOM 的 <see cref="Encoding.UTF8"/>。</para>
    /// </summary>
    private static byte[] EncodeWithPreamble(Encoding encoding, string content)
    {
        byte[] body = encoding.GetBytes(content);
        byte[] preamble = encoding.GetPreamble();
        if (preamble.Length == 0)
            return body;

        byte[] result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    /// <summary>
    /// 原子写盘核心实现：
    /// 1. 先写唯一 .tmp 临时文件，FileStream + WriteThrough + Flush(true) 强制 fsync 落盘
    /// 2. 原子替换主文件，并把旧主文件保留为 .bak：
    ///    Android: 两次 libc rename（主文件→.bak，.tmp→主文件），任一步失败回滚
    ///    其他平台: File.Replace，不支持时降级为可移植的备份+移动
    /// 进程崩溃/被杀时，主文件要么是完整旧值、要么是完整新值，不会出现半截文件。
    /// 注意：改名后不做目录 fsync，真正的掉电仍可能丢失 rename 的目录项，此时会回滚到 .bak。
    /// </summary>
    private static bool AtomicWrite(string path, byte[] data)
    {
        // 同一路径的并发原子写必须串行，否则 File.Replace 相互冲突、.bak 被交错覆盖
        lock (GetPathLock(path))
        {
            EnsureDirectoryExists(Path.GetDirectoryName(path));

            string tmp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + TmpSuffix;
            string bak = path + BakSuffix;

            try
            {
                WriteDurable(tmp, data);

                if (File.Exists(path))
                {
                    ReplaceWithBackup(tmp, path, bak);
                }
                else
                {
                    File.Move(tmp, path);
                }

                // 顺带清理崩溃遗留的旧 .tmp（只删 1 分钟以上的，不会误伤并发写入）
                TryCleanupStaleTmpFiles(path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileUtility] AtomicWrite 失败 [{path}]: {e.Message}");
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch (Exception cleanupEx)
                {
                    Debug.LogWarning($"[FileUtility] 清理临时文件失败 [{tmp}]: {cleanupEx.Message}");
                }
                return false;
            }
        }
    }

    /// <summary>
    /// 把 data 完整写入 path 并强制落盘（WriteThrough + Flush(true)），不做任何改名与备份。
    /// </summary>
    /// <param name="path">目标路径</param>
    /// <param name="data">要写入的内容</param>
    private static void WriteDurable(string path, byte[] data)
    {
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write,
                   FileShare.None, 4096, FileOptions.WriteThrough))
        {
            fs.Write(data, 0, data.Length);
            fs.Flush(true);
        }
    }

    /// <summary>
    /// 用 tmp 原地替换 path，且 <b>不</b>把旧文件保留为 .bak。
    /// <para>用于「主文件已损坏、需要就地修复」的场景：此时旧主文件是坏数据，
    /// 若像常规写入那样把它轮换成 .bak，就会覆盖掉唯一可用的好备份。</para>
    /// <para>Android 走 POSIX rename（原子覆盖）；其它平台走 File.Replace，不支持时降级为
    /// 「旧文件先改名 → 移入新文件 → 失败回滚」。</para>
    /// </summary>
    /// <param name="tmp">待安装的完整新内容</param>
    /// <param name="path">目标主文件路径</param>
    private static void SwapInPlace(string tmp, string path)
    {
        if (!File.Exists(path))
        {
            File.Move(tmp, path);
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (rename(tmp, path) != 0)
        {
            int errno = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            throw new IOException($"rename(tmp->path) failed, errno={errno}");
        }
#else
        try
        {
            File.Replace(tmp, path, null);
        }
        catch (Exception e) when (e is PlatformNotSupportedException
                                  || e is IOException
                                  || e is UnauthorizedAccessException
                                  || e is NotSupportedException
                                  || e is ArgumentException)
        {
            string aside = path + ".old." + Guid.NewGuid().ToString("N").Substring(0, 8);
            File.Move(path, aside);
            try
            {
                File.Move(tmp, path);
                DeleteFile(aside);
            }
            catch
            {
                File.Move(aside, path);
                throw;
            }
        }
#endif
    }

    /// <summary>
    /// 用 .tmp 原子替换主文件，并把旧主文件保留为 .bak（供安全读取链回滚）。
    /// </summary>
    private static void ReplaceWithBackup(string tmp, string path, string bak)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // POSIX rename 是原子覆盖，先把旧主文件转成备份，失败时回滚避免主文件丢失
        int ret = rename(path, bak);
        if (ret != 0)
        {
            int errno = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            throw new IOException($"rename(path->bak) failed, errno={errno}");
        }

        ret = rename(tmp, path);
        if (ret != 0)
        {
            int errno = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            rename(bak, path);
            throw new IOException($"rename(tmp->path) failed, errno={errno}");
        }
#else
        try
        {
            File.Replace(tmp, path, bak);
        }
        catch (Exception e) when (e is PlatformNotSupportedException
                                  || e is IOException
                                  || e is UnauthorizedAccessException
                                  || e is NotSupportedException
                                  || e is ArgumentException)
        {
            // 部分平台/文件系统（iOS 旧运行时、FAT32/exFAT、SMB 共享等）不支持 File.Replace，
            // 抛出的是 IOException / ArgumentException 而不是 PlatformNotSupportedException。
            // 这里统一降级为备份+移动；降级也失败时异常继续上抛，由 AtomicWrite 统一处理
            ReplacePortable(tmp, path, bak);
        }
#endif
    }

    /// <summary>
    /// 可移植的替换实现：先备份旧主文件，再移入新文件，失败时回滚。
    /// 存在极短的"主文件缺失"窗口，仅作为 File.Replace 不可用时的降级路径。
    /// </summary>
    private static void ReplacePortable(string tmp, string path, string bak)
    {
        if (File.Exists(bak))
            File.Delete(bak);

        File.Move(path, bak);
        try
        {
            File.Move(tmp, path);
        }
        catch
        {
            File.Move(bak, path);
            throw;
        }
    }

    #endregion

    #region 异步读取

    /// <summary>
    /// 异步读取文本文件内容，不阻塞UI线程。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>文件内容，读取失败返回null</returns>
    public static async Task<string> ReadAllTextAsync(string filePath, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllTextAsync: 文件路径为空");
            return null;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[FileUtility] ReadAllTextAsync: 文件不存在 [{filePath}]");
                return null;
            }

            encoding = encoding ?? DefaultEncoding;
            return await Task.Run(() => File.ReadAllText(filePath, encoding));
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] ReadAllTextAsync 失败 [{filePath}]: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 异步读取文件的所有字节，不阻塞UI线程。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件字节数组，读取失败返回null</returns>
    public static async Task<byte[]> ReadAllBytesAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllBytesAsync: 文件路径为空");
            return null;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[FileUtility] ReadAllBytesAsync: 文件不存在 [{filePath}]");
                return null;
            }

            return await Task.Run(() => File.ReadAllBytes(filePath));
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] ReadAllBytesAsync 失败 [{filePath}]: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 异步断电安全读取文本：按 主文件 -> .bak 备份 -> .tmp 残留 的顺序尝试读取。
    /// <para>.bak 是完整旧版本，优先于可能半写的 .tmp。</para>
    /// <para>主文件存在但读不出来（被占用、无权限）时直接返回 null，不回滚到旧备份——
    /// 避免把瞬时占用误判成内容损坏并静默返回过期数据。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码格式，默认UTF-8（无BOM）</param>
    /// <param name="validate">内容校验委托，返回 false 视为该副本损坏并继续尝试下一个</param>
    /// <returns>文件内容，全部失败返回null</returns>
    public static async Task<string> ReadAllTextSafeAsync(string filePath, Encoding encoding = null, Func<string, bool> validate = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllTextSafeAsync: 文件路径为空");
            return null;
        }

        encoding = encoding ?? DefaultEncoding;

        ReadResult<string> main = await TryReadExistingAsync(filePath, path => Task.Run(() => File.ReadAllText(path, encoding)));
        if (main.Outcome == ReadOutcome.Failed)
            return null;
        if (main.Outcome == ReadOutcome.Ok && IsValid(main.Value, validate))
            return main.Value;

        string bak = filePath + BakSuffix;
        ReadResult<string> backup = await TryReadExistingAsync(bak, path => Task.Run(() => File.ReadAllText(path, encoding)));
        if (backup.Outcome == ReadOutcome.Failed)
            return null;
        if (backup.Outcome == ReadOutcome.Ok && IsValid(backup.Value, validate))
        {
            Debug.LogWarning($"[FileUtility] 主文件损坏，已从备份恢复 [{Path.GetFileName(filePath)}]");
            string bakContent = backup.Value;
            byte[] repairBytes = EncodeWithPreamble(encoding, bakContent);
            await Task.Run(() => RepairMainFileWithBytes(filePath, repairBytes));
            return bakContent;
        }

        return await Task.Run(() => TryRecoverFromTmp(filePath, path => File.ReadAllText(path, encoding), validate));
    }

    /// <summary>
    /// 异步断电安全读取多行文本：按 主文件 -> .bak 备份 -> .tmp 残留 的顺序尝试读取。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码格式，默认UTF-8（无BOM）</param>
    /// <param name="validate">内容校验委托，返回 false 视为该副本损坏并继续尝试下一个</param>
    /// <returns>文件各行内容，全部失败返回null</returns>
    public static async Task<string[]> ReadAllLinesSafeAsync(string filePath, Encoding encoding = null, Func<string[], bool> validate = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllLinesSafeAsync: 文件路径为空");
            return null;
        }

        encoding = encoding ?? DefaultEncoding;

        ReadResult<string[]> main = await TryReadExistingAsync(filePath, path => Task.Run(() => File.ReadAllLines(path, encoding)));
        if (main.Outcome == ReadOutcome.Failed)
            return null;
        if (main.Outcome == ReadOutcome.Ok && IsValid(main.Value, validate))
            return main.Value;

        string bak = filePath + BakSuffix;
        ReadResult<string[]> backup = await TryReadExistingAsync(bak, path => Task.Run(() => File.ReadAllLines(path, encoding)));
        if (backup.Outcome == ReadOutcome.Failed)
            return null;
        if (backup.Outcome == ReadOutcome.Ok && IsValid(backup.Value, validate))
        {
            Debug.LogWarning($"[FileUtility] 主文件损坏，已从备份恢复 [{Path.GetFileName(filePath)}]");
            string[] bakLines = backup.Value;
            byte[] repairBytes = EncodeWithPreamble(encoding, JoinLines(bakLines));
            await Task.Run(() => RepairMainFileWithBytes(filePath, repairBytes));
            return bakLines;
        }

        return await Task.Run(() => TryRecoverFromTmp(filePath, path => File.ReadAllLines(path, encoding), validate));
    }

    /// <summary>
    /// 异步断电安全读取字节：按 主文件 -> .bak 备份 -> .tmp 残留 的顺序尝试读取。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="validate">内容校验委托，返回 false 视为该副本损坏并继续尝试下一个</param>
    /// <returns>文件字节内容，全部失败返回null</returns>
    public static async Task<byte[]> ReadAllBytesSafeAsync(string filePath, Func<byte[], bool> validate = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] ReadAllBytesSafeAsync: 文件路径为空");
            return null;
        }

        ReadResult<byte[]> main = await TryReadExistingAsync(filePath, path => Task.Run(() => File.ReadAllBytes(path)));
        if (main.Outcome == ReadOutcome.Failed)
            return null;
        if (main.Outcome == ReadOutcome.Ok && IsValid(main.Value, validate))
            return main.Value;

        string bak = filePath + BakSuffix;
        ReadResult<byte[]> backup = await TryReadExistingAsync(bak, path => Task.Run(() => File.ReadAllBytes(path)));
        if (backup.Outcome == ReadOutcome.Failed)
            return null;
        if (backup.Outcome == ReadOutcome.Ok && IsValid(backup.Value, validate))
        {
            Debug.LogWarning($"[FileUtility] 主文件损坏，已从备份恢复 [{Path.GetFileName(filePath)}]");
            byte[] bakData = backup.Value;
            await Task.Run(() => RepairMainFileWithBytes(filePath, bakData));
            return bakData;
        }

        return await Task.Run(() => TryRecoverFromTmp(filePath, File.ReadAllBytes, validate));
    }

    /// <summary>异步读取结果：把「不存在」和「读失败」区分开</summary>
    private struct ReadResult<T>
    {
        /// <summary>读取结果</summary>
        public ReadOutcome Outcome;
        /// <summary>读到的内容</summary>
        public T Value;
    }

    /// <summary>
    /// 异步读取候选文件，语义与 <see cref="TryReadExisting"/> 一致：
    /// 不存在返回 Missing（调用方继续回滚），读失败返回 Failed（调用方必须直接失败，不回滚）。
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="reader">异步读取委托</param>
    /// <returns>读取结果</returns>
    private static async Task<ReadResult<T>> TryReadExistingAsync<T>(string path, Func<string, Task<T>> reader)
    {
        ReadResult<T> result = new ReadResult<T>();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            result.Outcome = ReadOutcome.Missing;
            return result;
        }

        try
        {
            result.Value = await reader(path);
            result.Outcome = ReadOutcome.Ok;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] 异步读取失败（按环境故障处理，不回退备份） [{Path.GetFileName(path)}]: {e.Message}");
            result.Outcome = ReadOutcome.Failed;
        }

        return result;
    }

    #endregion

    #region 异步写入

    /// <summary>
    /// 异步写入文本到文件（普通模式，非原子），不阻塞UI线程。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>是否写入成功</returns>
    public static async Task<bool> WriteAllTextAsync(string filePath, string content, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllTextAsync: 文件路径为空");
            return false;
        }

        if (content == null)
        {
            Debug.LogError("[FileUtility] WriteAllTextAsync: 内容为空");
            return false;
        }

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(filePath));
            encoding = encoding ?? DefaultEncoding;
            await Task.Run(() => File.WriteAllText(filePath, content, encoding));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllTextAsync 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 异步写入字节数组到文件（普通模式，非原子），不阻塞UI线程。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要写入的字节数组</param>
    /// <returns>是否写入成功</returns>
    public static async Task<bool> WriteAllBytesAsync(string filePath, byte[] data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllBytesAsync: 文件路径为空");
            return false;
        }

        if (data == null)
        {
            Debug.LogError("[FileUtility] WriteAllBytesAsync: 数据为空");
            return false;
        }

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(filePath));
            await Task.Run(() => File.WriteAllBytes(filePath, data));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllBytesAsync 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 异步原子写入文本：先写 .tmp，再 File.Replace 原子改名。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码格式，默认UTF-8</param>
    /// <returns>是否写入成功</returns>
    public static async Task<bool> WriteAllTextAtomicAsync(string filePath, string content, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllTextAtomicAsync: 文件路径为空");
            return false;
        }

        if (content == null)
        {
            Debug.LogError("[FileUtility] WriteAllTextAtomicAsync: 内容为空");
            return false;
        }

        try
        {
            encoding = encoding ?? DefaultEncoding;
            byte[] data = EncodeWithPreamble(encoding, content);
            return await Task.Run(() => AtomicWrite(filePath, data));
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllTextAtomicAsync 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 异步写入行数组到文件（普通模式，非原子），不阻塞UI线程。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="lines">要写入的行数组</param>
    /// <param name="encoding">编码格式，默认UTF-8（无BOM）</param>
    /// <returns>是否写入成功</returns>
    public static async Task<bool> WriteAllLinesAsync(string filePath, string[] lines, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllLinesAsync: 文件路径为空");
            return false;
        }

        if (lines == null)
        {
            Debug.LogError("[FileUtility] WriteAllLinesAsync: 内容数组为空");
            return false;
        }

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(filePath));
            encoding = encoding ?? DefaultEncoding;
            await Task.Run(() => File.WriteAllLines(filePath, lines, encoding));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllLinesAsync 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 异步原子写入行数组：先写 .tmp，再 File.Replace 原子改名。
    /// <para>与 <see cref="WriteAllLinesAtomic"/> 行为一致，每行末尾带换行符（末行也带）。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="lines">要写入的行数组</param>
    /// <param name="encoding">编码格式，默认UTF-8（无BOM）</param>
    /// <returns>是否写入成功</returns>
    public static async Task<bool> WriteAllLinesAtomicAsync(string filePath, string[] lines, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllLinesAtomicAsync: 文件路径为空");
            return false;
        }

        if (lines == null)
        {
            Debug.LogError("[FileUtility] WriteAllLinesAtomicAsync: 内容数组为空");
            return false;
        }

        try
        {
            encoding = encoding ?? DefaultEncoding;
            string content = lines.Length > 0
                ? string.Join(Environment.NewLine, lines) + Environment.NewLine
                : string.Empty;

            byte[] data = EncodeWithPreamble(encoding, content);
            return await Task.Run(() => AtomicWrite(filePath, data));
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllLinesAtomicAsync 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 异步原子写入字节数组：先写 .tmp，再 File.Replace 原子改名。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要写入的字节数组</param>
    /// <returns>是否写入成功</returns>
    public static async Task<bool> WriteAllBytesAtomicAsync(string filePath, byte[] data)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] WriteAllBytesAtomicAsync: 文件路径为空");
            return false;
        }

        if (data == null)
        {
            Debug.LogError("[FileUtility] WriteAllBytesAtomicAsync: 数据为空");
            return false;
        }

        try
        {
            return await Task.Run(() => AtomicWrite(filePath, data));
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] WriteAllBytesAtomicAsync 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    #endregion

    /// <summary>
    /// 文件拷贝进度信息。
    /// </summary>
    public struct FileCopyProgress
    {
        /// <summary>已拷贝字节数</summary>
        public long bytesCopied;
        /// <summary>文件总字节数</summary>
        public long totalBytes;
        /// <summary>进度 0.0 ~ 1.0</summary>
        public float progress;
    }

    #region 文件操作

    /// <summary>
    /// 删除文件。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否删除成功</returns>
    public static bool DeleteFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] DeleteFile: 文件路径为空");
            return false;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] DeleteFile 失败 [{filePath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 删除文件及其断电安全机制产生的附属文件（.bak 备份与残留 .tmp）。
    /// <para>仅删除主文件时，<see cref="ReadAllTextSafe"/> / <see cref="ReadAllBytesSafe"/> 会在主文件缺失时
    /// 从 .bak 或残留 .tmp 回滚，导致已删除的数据再次被读取到。使用本方法才能真正删除。</para>
    /// <para>主文件本来就不存在时也返回 true（与 <see cref="DeleteFile"/> 的语义不同）。</para>
    /// <para>主文件已删除但 .bak / .tmp 未清理干净时返回 false，便于调用方发现异常残留。</para>
    /// <para>与原子写入共用同一路径锁，避免"删除与写入并发"时出现文件复活或误删新数据。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>主文件已不存在或删除成功返回 true，删除失败返回 false</returns>
    public static bool DeleteFileWithBackup(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[FileUtility] DeleteFileWithBackup: 文件路径为空");
            return false;
        }

        lock (GetPathLock(filePath))
        {
            // 主文件删除失败（被占用等）时保留备份，避免数据意外丢失
            if (!DeleteFile(filePath) && File.Exists(filePath))
            {
                return false;
            }

            // 附属文件删除失败不影响「主文件已删除」的事实，但结果要如实反映，否则调用方会误以为已彻底清理
            bool bakDeleted = DeleteFile(filePath + BakSuffix) || !File.Exists(filePath + BakSuffix);
            CleanupTmpFiles(filePath);

            // GetTmpFiles 枚举失败时返回 null（目录不存在则返回空数组），null 表示无法确认是否清理干净
            string[] remainingTmpFiles = GetTmpFiles(filePath);
            bool tmpCleared = remainingTmpFiles != null && remainingTmpFiles.Length == 0;

            if (!bakDeleted || !tmpCleared)
            {
                Debug.LogWarning($"[FileUtility] DeleteFileWithBackup: 主文件已删除，但附属文件未清理干净 [{filePath}]");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 复制文件。
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destinationPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    /// <returns>是否复制成功</returns>
    public static bool CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            Debug.LogError("[FileUtility] CopyFile: 路径不能为空");
            return false;
        }

        try
        {
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[FileUtility] CopyFile: 源文件不存在 [{sourcePath}]");
                return false;
            }

            EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath, overwrite);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] CopyFile 失败 [{sourcePath}] -> [{destinationPath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 异步复制文件，不阻塞UI线程，支持进度回调。
    /// 所有回调（onProgress/onCompleted/onError）均通过 LoomManager 自动调度到主线程，
    /// 可直接操作 Unity UI 组件；派发失败时只记警告，绝不让异常从本方法逃出去。
    /// onProgress 内置节流（约100ms一次），避免过多刷新。
    /// <para>建议在主线程调用：首次创建 LoomManager 需要主线程，非主线程调用时回调会被丢弃。</para>
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destinationPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    /// <param name="onProgress">进度回调（主线程）</param>
    /// <param name="onCompleted">拷贝成功回调（主线程）</param>
    /// <param name="onError">拷贝失败回调，参数为错误信息（主线程）</param>
    /// <param name="bufferSize">缓冲区大小（字节），默认 8192</param>
    /// <returns>是否复制成功</returns>
    public static async Task<bool> CopyFileAsync(string sourcePath, string destinationPath,
        bool overwrite = false,
        Action<FileCopyProgress> onProgress = null,
        Action onCompleted = null,
        Action<string> onError = null,
        int bufferSize = 8192)
    {
        // 预热主线程调度器：LoomManager 首次访问会创建 GameObject，必须在主线程完成。
        // 只在确实有回调时预热——为了拷一个文件就往场景里塞一个常驻 GameObject 并不合适。
        bool hasCallback = onProgress != null || onCompleted != null || onError != null;
        if (hasCallback)
            EnsureLoomManagerWarmedUp();

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            string msg = "路径不能为空";
            Debug.LogError($"[FileUtility] CopyFileAsync: {msg}");
            SafeQueueOnMainThread(() => onError?.Invoke(msg));
            return false;
        }

        try
        {
            if (!File.Exists(sourcePath))
            {
                string msg = $"源文件不存在 [{sourcePath}]";
                Debug.LogWarning($"[FileUtility] CopyFileAsync: {msg}");
                SafeQueueOnMainThread(() => onError?.Invoke(msg));
                return false;
            }

            if (!overwrite && File.Exists(destinationPath))
            {
                string msg = $"目标文件已存在且未开启覆盖 [{destinationPath}]";
                Debug.LogWarning($"[FileUtility] CopyFileAsync: {msg}");
                SafeQueueOnMainThread(() => onError?.Invoke(msg));
                return false;
            }

            EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));

            // 先完整写入临时文件，成功后再原子替换目标：
            // 覆盖模式不会提前截断/删除已有目标，中途失败也不会让目标变成半截文件
            string tempPath = destinationPath + PartialSuffix + Guid.NewGuid().ToString("N").Substring(0, 8);
            int streamBufferSize = Math.Max(bufferSize, 4096);

            await Task.Run(() =>
            {
                try
                {
                    long totalBytes = new FileInfo(sourcePath).Length;
                    long bytesCopied = 0;
                    byte[] buffer = new byte[streamBufferSize];
                    DateTime lastReportTime = DateTime.MinValue;
                    const int reportIntervalMs = 100;

                    using (FileStream sourceStream = File.OpenRead(sourcePath))
                    using (FileStream destStream = new FileStream(tempPath, FileMode.Create,
                        FileAccess.Write, FileShare.None, streamBufferSize))
                    {
                        int bytesRead;
                        while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            destStream.Write(buffer, 0, bytesRead);
                            bytesCopied += bytesRead;

                            if (onProgress != null)
                            {
                                DateTime now = DateTime.UtcNow;
                                bool isLastChunk = bytesCopied >= totalBytes;
                                if (isLastChunk || (now - lastReportTime).TotalMilliseconds >= reportIntervalMs)
                                {
                                    lastReportTime = now;
                                    FileCopyProgress progress = new FileCopyProgress
                                    {
                                        bytesCopied = bytesCopied,
                                        totalBytes = totalBytes,
                                        progress = totalBytes > 0 ? (float)bytesCopied / totalBytes : 1f
                                    };
                                    SafeQueueOnMainThread(() => onProgress(progress));
                                }
                            }
                        }

                        // 内容落盘后再改名，避免改名成功但数据还留在页缓存里
                        destStream.Flush(true);
                    }

                    // 与原子写入共用同一路径锁：替换那一刻目标要么还是完整旧值、要么已是完整新值
                    lock (GetPathLock(destinationPath))
                    {
                        if (File.Exists(destinationPath))
                            ReplaceWithBackup(tempPath, destinationPath, destinationPath + BakSuffix);
                        else
                            File.Move(tempPath, destinationPath);
                    }
                }
                catch
                {
                    // 失败只清理临时文件，绝不动已经存在的目标文件
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.LogWarning($"[FileUtility] 清理复制临时文件失败 [{tempPath}]: {cleanupEx.Message}");
                    }
                    throw;
                }
            });

            SafeQueueOnMainThread(() => onCompleted?.Invoke());
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] CopyFileAsync 失败 [{sourcePath}] -> [{destinationPath}]: {e.Message}");
            SafeQueueOnMainThread(() => onError?.Invoke(e.Message));
            return false;
        }
    }

    /// <summary>
    /// 把回调派发到主线程。派发本身失败（典型是方法在非主线程被调用、LoomManager 无法创建）时
    /// 只记录警告并丢弃回调，绝不让异常从文件 API 里逃出去。
    /// </summary>
    /// <param name="action">要派发的回调</param>
    private static void SafeQueueOnMainThread(Action action)
    {
        if (action == null)
            return;

        try
        {
            LoomManager.QueueOnMainThread(action);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FileUtility] 回调派发到主线程失败（请在主线程调用本 API）: {e.Message}");
        }
    }

    /// <summary>
    /// 预热 LoomManager，确保其懒加载创建 GameObject 的过程发生在主线程。
    /// 已存在实例时为纯字段读取，开销可忽略。
    /// </summary>
    private static void EnsureLoomManagerWarmedUp()
    {
        try
        {
            _ = LoomManager.Instance;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FileUtility] LoomManager 预热失败: {e.Message}");
        }
    }

    /// <summary>
    /// 移动文件。
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destinationPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的目标文件，默认 false 以免误删数据</param>
    /// <returns>是否移动成功</returns>
    public static bool MoveFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            Debug.LogError("[FileUtility] MoveFile: 路径不能为空");
            return false;
        }

        try
        {
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[FileUtility] MoveFile: 源文件不存在 [{sourcePath}]");
                return false;
            }

            EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));

            // 同时持有源与目标的路径锁：
            // 1) 防止并发的原子写正在为源路径写 .tmp 时，下面的 CleanupTmpFiles 把那份临时文件删掉
            // 2) 目标已存在时要用 File.Replace 覆盖、还要迁移目标的 .bak，必须与目标的原子写串行
            bool moved = false;
            WithPathLocks(sourcePath, destinationPath, () =>
            {
                if (!MoveFileCore(sourcePath, destinationPath, overwrite))
                    return;

                // 备份文件跟随主文件一起搬走，否则新路径的安全读取链没有可用备份
                MoveBackupFile(sourcePath, destinationPath);
                CleanupTmpFiles(sourcePath);
                moved = true;
            });
            return moved;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] MoveFile 失败 [{sourcePath}] -> [{destinationPath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 移动主文件：优先原子替换，跨卷/不支持时降级为「复制成功后再删源」，最坏情况是两份都在，不丢数据。
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destinationPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的目标文件</param>
    /// <returns>是否移动成功</returns>
    private static bool MoveFileCore(string sourcePath, string destinationPath, bool overwrite)
    {
        bool destinationExists = File.Exists(destinationPath);

        if (destinationExists && !overwrite)
        {
            Debug.LogWarning($"[FileUtility] 目标文件已存在且未开启覆盖 [{destinationPath}]");
            return false;
        }

        // 覆盖时优先单步原子替换：不会出现「目标已删、源还没到」的中间态
        if (destinationExists)
        {
            try
            {
                File.Replace(sourcePath, destinationPath, null, true);
                return true;
            }
            catch (Exception)
            {
                // 跨卷或文件系统不支持 Replace，继续走下面的降级路径
            }
        }

        try
        {
            File.Move(sourcePath, destinationPath);
            return true;
        }
        catch (Exception)
        {
            // 跨卷移动（如内部存储 -> SD 卡）不支持 rename，降级为复制 + 删源
            File.Copy(sourcePath, destinationPath, true);
            File.Delete(sourcePath);
            return true;
        }
    }

    /// <summary>
    /// 把源文件的 .bak 备份迁移到目标路径，保证新路径仍具备回滚能力。
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destinationPath">目标文件路径</param>
    private static void MoveBackupFile(string sourcePath, string destinationPath)
    {
        string sourceBak = sourcePath + BakSuffix;
        if (!File.Exists(sourceBak))
            return;

        string destinationBak = destinationPath + BakSuffix;
        string aside = null;
        try
        {
            // 目标已有备份时先挪到一边而不是直接删除，移动失败还能原样还原
            if (File.Exists(destinationBak))
            {
                aside = destinationBak + ".old." + Guid.NewGuid().ToString("N").Substring(0, 8);
                File.Move(destinationBak, aside);
            }

            File.Move(sourceBak, destinationBak);

            if (aside != null)
                DeleteFile(aside);
        }
        catch (Exception e)
        {
            // 备份迁移失败不影响主文件已移动成功的结果，仅记录警告
            Debug.LogWarning($"[FileUtility] MoveFile: 备份迁移失败 [{sourceBak}] -> [{destinationBak}]: {e.Message}");

            if (aside == null)
                return;

            try
            {
                // 还原目标原有的备份，避免「移动失败 + 旧备份被删」双重损失
                if (!File.Exists(destinationBak) && File.Exists(aside))
                    File.Move(aside, destinationBak);
            }
            catch (Exception restoreEx)
            {
                Debug.LogWarning($"[FileUtility] MoveFile: 还原目标备份失败 [{aside}]: {restoreEx.Message}");
            }
        }
    }

    /// <summary>
    /// 检查文件是否存在。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件是否存在</returns>
    public static bool FileExists(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        return File.Exists(filePath);
    }

    /// <summary>
    /// 获取文件大小（字节）。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件大小，文件不存在返回-1</returns>
    public static long GetFileSize(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return -1;

        try
        {
            if (!File.Exists(filePath))
                return -1;

            FileInfo fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] GetFileSize 失败 [{filePath}]: {e.Message}");
            return -1;
        }
    }

    #endregion

    #region 目录操作

    /// <summary>
    /// 检查目录是否存在。
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <returns>目录是否存在</returns>
    public static bool DirectoryExists(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
            return false;

        return Directory.Exists(directoryPath);
    }

    /// <summary>
    /// 创建目录（如果不存在）。
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <returns>是否创建成功</returns>
    public static bool CreateDirectory(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            Debug.LogError("[FileUtility] CreateDirectory: 目录路径为空");
            return false;
        }

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] CreateDirectory 失败 [{directoryPath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 删除目录。
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="recursive">是否递归删除子目录和文件</param>
    /// <returns>是否删除成功</returns>
    public static bool DeleteDirectory(string directoryPath, bool recursive = false)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            Debug.LogError("[FileUtility] DeleteDirectory: 目录路径为空");
            return false;
        }

        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive);
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] DeleteDirectory 失败 [{directoryPath}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取目录中的所有文件路径。
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="searchPattern">搜索模式（如 "*.txt"）</param>
    /// <param name="searchOption">搜索选项</param>
    /// <returns>文件路径数组</returns>
    public static string[] GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            Debug.LogError("[FileUtility] GetFiles: 目录路径为空");
            return new string[0];
        }

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogWarning($"[FileUtility] GetFiles: 目录不存在 [{directoryPath}]");
                return new string[0];
            }

            return Directory.GetFiles(directoryPath, searchPattern, searchOption);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] GetFiles 失败 [{directoryPath}]: {e.Message}");
            return new string[0];
        }
    }

    /// <summary>
    /// 获取目录中的所有子目录路径。
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="searchPattern">搜索模式</param>
    /// <param name="searchOption">搜索选项</param>
    /// <returns>子目录路径数组</returns>
    public static string[] GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            Debug.LogError("[FileUtility] GetDirectories: 目录路径为空");
            return new string[0];
        }

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogWarning($"[FileUtility] GetDirectories: 目录不存在 [{directoryPath}]");
                return new string[0];
            }

            return Directory.GetDirectories(directoryPath, searchPattern, searchOption);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] GetDirectories 失败 [{directoryPath}]: {e.Message}");
            return new string[0];
        }
    }

    #endregion

    #region 路径处理

    /// <summary>
    /// 合并多个路径片段，自动跳过 null/空片段。
    /// </summary>
    /// <param name="paths">路径片段数组</param>
    /// <returns>合并后的路径，全部片段为空时返回空字符串</returns>
    public static string CombinePaths(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return string.Empty;

        string result = null;
        foreach (string part in paths)
        {
            // Path.Combine 遇到 null/空片段会抛异常，这里直接跳过
            if (string.IsNullOrEmpty(part))
                continue;

            result = result == null ? part : Path.Combine(result, part);
        }
        return result ?? string.Empty;
    }

    /// <summary>
    /// 获取文件的扩展名。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>扩展名（包含点号）</returns>
    public static string GetExtension(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        return Path.GetExtension(filePath);
    }

    /// <summary>
    /// 获取文件名（不包含路径）。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件名</returns>
    public static string GetFileName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        return Path.GetFileName(filePath);
    }

    /// <summary>
    /// 获取文件名（不包含路径和扩展名）。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件名（不含扩展名）</returns>
    public static string GetFileNameWithoutExtension(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// 获取文件所在目录路径。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>目录路径</returns>
    public static string GetDirectoryPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        return Path.GetDirectoryName(filePath) ?? string.Empty;
    }

    /// <summary>
    /// 将相对路径转换为绝对路径。
    /// <para>路径非法或过长时回退为直接拼接，不抛异常（保证本方法可用于任意用户输入）。</para>
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <param name="basePath">基准路径，默认为Application.dataPath</param>
    /// <returns>绝对路径</returns>
    public static string ToAbsolutePath(string relativePath, string basePath = null)
    {
        if (string.IsNullOrEmpty(relativePath))
            return string.Empty;

        if (Path.IsPathRooted(relativePath))
            return relativePath;

        basePath = basePath ?? Application.dataPath;

        try
        {
            return Path.GetFullPath(Path.Combine(basePath, relativePath));
        }
        catch (Exception e) when (e is ArgumentException || e is PathTooLongException || e is NotSupportedException)
        {
            Debug.LogWarning($"[FileUtility] ToAbsolutePath 路径不合法，已回退为拼接路径 [{relativePath}]: {e.Message}");
            return Path.Combine(basePath, relativePath);
        }
    }

    #endregion

    #region 文件搜索

    /// <summary>
    /// 在目录中搜索包含指定文本的文件。
    /// <para>逐文件逐行读取，避免一次把整个文件读入内存；单个文件读取失败会跳过而不中断整个搜索。</para>
    /// <para>同步阻塞方法，建议在编辑器工具或非热路径中使用。</para>
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="searchText">搜索文本</param>
    /// <param name="searchPattern">文件匹配模式（如 "*.cs"）</param>
    /// <returns>包含指定文本的文件路径列表</returns>
    public static List<string> SearchFilesByContent(string directoryPath, string searchText, string searchPattern = "*")
    {
        List<string> result = new List<string>();

        if (string.IsNullOrEmpty(directoryPath) || string.IsNullOrEmpty(searchText))
            return result;

        string[] files;
        try
        {
            files = GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] SearchFilesByContent 枚举文件失败: {e.Message}");
            return result;
        }

        foreach (string file in files)
        {
            try
            {
                // 逐行读取，遇到匹配立即停止，避免大文件全量驻留内存
                using (var reader = new StreamReader(file, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains(searchText))
                        {
                            result.Add(file);
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 单个文件被占用/无法读取时跳过，不影响其它文件
            }
        }

        return result;
    }

    /// <summary>
    /// 在目录中搜索文件名包含指定文本的文件。
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="fileNameText">文件名包含的文本</param>
    /// <returns>匹配的文件路径列表</returns>
    public static List<string> SearchFilesByName(string directoryPath, string fileNameText)
    {
        List<string> result = new List<string>();

        if (string.IsNullOrEmpty(directoryPath) || string.IsNullOrEmpty(fileNameText))
            return result;

        try
        {
            string[] files = GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string fileName = GetFileName(file);
                if (fileName.IndexOf(fileNameText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(file);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] SearchFilesByName 失败: {e.Message}");
        }

        return result;
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 确保目录存在，如果不存在则创建。
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    /// <summary>
    /// 获取文件的MD5哈希值。
    /// <para>同步读取整个文件，大文件会阻塞调用线程，主线程请使用异步版本。</para>
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>MD5哈希字符串，失败返回null</returns>
    public static string GetFileMD5(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BytesToHex(hash);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] GetFileMD5 失败 [{filePath}]: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 异步获取文件的MD5哈希值，哈希计算在后台线程执行，不阻塞主线程。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>MD5哈希字符串，失败返回null</returns>
    public static Task<string> GetFileMD5Async(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return Task.FromResult<string>(null);

        return Task.Run(() => GetFileMD5(filePath));
    }

    /// <summary>将字节数组转换为小写十六进制字符串</summary>
    private static string BytesToHex(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
        {
            sb.Append(bytes[i].ToString("x2"));
        }
        return sb.ToString();
    }

    /// <summary>
    /// 获取文件的最后修改时间。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>最后修改时间，失败返回DateTime.MinValue</returns>
    public static DateTime GetLastWriteTime(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return DateTime.MinValue;

        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            return fileInfo.LastWriteTime;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] GetLastWriteTime 失败 [{filePath}]: {e.Message}");
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// 安全地重命名文件。
    /// </summary>
    /// <param name="filePath">原文件路径</param>
    /// <param name="newFileName">新文件名（不含路径）</param>
    /// <returns>是否重命名成功</returns>
    public static bool RenameFile(string filePath, string newFileName)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(newFileName))
        {
            Debug.LogError("[FileUtility] RenameFile: 参数不能为空");
            return false;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[FileUtility] RenameFile: 文件不存在 [{filePath}]");
                return false;
            }

            // newFileName 的契约是「纯文件名」，必须校验：否则 Path.Combine 遇到根路径会丢弃目录部分，
            // "..\\..\\x" 也能把文件搬到目录之外（路径穿越）
            if (!IsValidFileName(newFileName))
            {
                Debug.LogError($"[FileUtility] RenameFile: 非法的新文件名 [{newFileName}]");
                return false;
            }

            string directory = GetDirectoryPath(filePath);
            string newPath = CombinePaths(directory, newFileName);

            // 重命名语义不允许覆盖同名文件，统一走 MoveFileCore 保证移动失败不丢数据
            // 与 MoveFile 一样同时持有源与目标路径锁：新名字可能正是另一个线程正在原子写的目标
            bool renamed = false;
            WithPathLocks(filePath, newPath, () =>
            {
                if (!MoveFileCore(filePath, newPath, false))
                    return;

                // 备份文件跟随主文件一起改名，否则新名字的安全读取链没有可用备份
                MoveBackupFile(filePath, newPath);
                CleanupTmpFiles(filePath);
                renamed = true;
            });
            return renamed;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FileUtility] RenameFile 失败 [{filePath}] -> [{newFileName}]: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 校验「纯文件名」：拒绝根路径、目录分隔符、非法字符以及 "." / ".."。
    /// <para>用于防止 Path.Combine 被根路径覆盖，以及通过 .. 实现路径穿越。</para>
    /// </summary>
    /// <param name="fileName">待校验的文件名</param>
    /// <returns>是合法的纯文件名返回 true</returns>
    private static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName == "." || fileName == "..")
            return false;

        if (Path.IsPathRooted(fileName))
            return false;

        // Windows 下已包含 '\' '/' ':'，其它平台按各自规则返回
        return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    #endregion
}