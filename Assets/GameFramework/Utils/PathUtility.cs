using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 通用路径工具类。
/// 提供路径规范化、安全检查、文件名处理、常用目录获取等功能。
/// 与 FileUtility 互补：FileUtility 侧重文件 I/O，PathUtility 侧重路径本身的操作和校验。
/// </summary>
/// <example>路径规范化
/// <code>
/// string normalized = PathUtility.Normalize("C:\\A//B\\C");     // C:\A\B\C
/// string relative = PathUtility.GetRelativePath(base, target);  // 获取相对路径
/// bool isChild = PathUtility.IsSubPath("C:/Game", "C:/Game/Data/save.json"); // true
/// </code>
/// </example>
/// <example>路径安全检查
/// <code>
/// // 信任的根目录
/// string trustedRoot = Application.persistentDataPath;
/// string userInput = "saves/../../system/config";
///
/// // 先规范化为完整路径，再校验
/// string safe = PathUtility.SafeResolve(userInput, trustedRoot);
/// if (safe != null)
/// {
///     // 安全，可以操作
/// }
/// </code>
/// </example>
/// <example>文件名处理
/// <code>
/// string valid = PathUtility.SanitizeFileName("my:file?.txt");  // my_file_.txt
/// bool isValid = PathUtility.IsValidFileName("data.json");      // true
/// </code>
/// </example>
/// <example>常用目录
/// <code>
/// string dataDir = PathUtility.PersistentDataPath;       // Application.persistentDataPath
/// string cacheDir = PathUtility.CachePath;                // Application.temporaryCachePath
/// string streamDir = PathUtility.StreamingAssetsPath;    // Application.streamingAssetsPath
/// string savePath = PathUtility.Combine(PathUtility.PersistentDataPath, "Saves", "slot1.dat");
/// </code>
/// </example>
/// <example>路径操作
/// <code>
/// string changed = PathUtility.ChangeExtension("data.json", ".dat"); // data.dat
/// string unique = PathUtility.GetUniqueFilePath("result.txt");        // result (1).txt
/// string unified = PathUtility.UseForwardSlash("Assets\\Textures");   // Assets/Textures
/// </code>
/// </example>
public static class PathUtility
{
    #region 常用目录

    /// <summary>
    /// 持久化数据目录（如 C:/Users/xxx/AppData/LocalLow/Company/Product）。
    /// 适合存放存档、用户配置等需要持久保留的数据。
    /// </summary>
    public static string PersistentDataPath => Application.persistentDataPath;

    /// <summary>
    /// 临时缓存目录，系统可能随时清理。
    /// 适合存放可重建的临时文件、下载缓存等。
    /// </summary>
    public static string CachePath => Application.temporaryCachePath;

    /// <summary>
    /// StreamingAssets 目录（只读）。
    /// 适合存放随包发布的配置文件、初始数据等。
    /// 注意：Android 下此目录在 .apk 内，需通过 UnityWebRequest 或 WWW 读取。
    /// </summary>
    public static string StreamingAssetsPath => Application.streamingAssetsPath;

    /// <summary>
    /// 项目 DataPath（包含 Assets 文件夹的上级目录）。
    /// 编辑器中为项目根目录，运行时为可执行文件所在目录。
    /// </summary>
    public static string DataPath => Application.dataPath;

    #endregion

    #region 路径拼接

    /// <summary>
    /// 合并多个路径片段。
    /// </summary>
    /// <param name="paths">路径片段</param>
    /// <returns>合并后的路径</returns>
    public static string Combine(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return string.Empty;

        string result = paths[0];
        for (int i = 1; i < paths.Length; i++)
        {
            result = Path.Combine(result, paths[i]);
        }
        return Normalize(result);
    }

    #endregion

    #region 路径规范化

    /// <summary>
    /// 将路径中的分隔符统一为当前平台的目录分隔符（Windows: \，其他: /）。
    /// 同时移除末尾多余的空白和分隔符。
    /// </summary>
    /// <param name="path">原始路径</param>
    /// <returns>规范化后的路径</returns>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        path = path.Replace('/', Path.DirectorySeparatorChar)
                   .Replace('\\', Path.DirectorySeparatorChar)
                   .Trim();

        if (path.Length > 1 && path[path.Length - 1] == Path.DirectorySeparatorChar
                            && path[path.Length - 2] != ':')
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
        }

        return path;
    }

    /// <summary>
    /// 将路径中的分隔符统一为正斜杠（/），适合 Unity 资源路径、WWW/WebRequest。
    /// </summary>
    /// <param name="path">原始路径</param>
    /// <returns>正斜杠路径</returns>
    public static string UseForwardSlash(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return path.Replace('\\', '/').Trim();
    }

    /// <summary>
    /// 将路径分隔符统一为反斜杠（\），适合 Windows 原生 API。
    /// </summary>
    /// <param name="path">原始路径</param>
    /// <returns>反斜杠路径</returns>
    public static string UseBackSlash(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return path.Replace('/', '\\').Trim();
    }

    /// <summary>
    /// 确保路径以指定目录分隔符结尾。常用于"确保是目录路径"的场景。
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>以分隔符结尾的路径</returns>
    public static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Path.DirectorySeparatorChar.ToString();

        if (path[path.Length - 1] != Path.DirectorySeparatorChar
            && path[path.Length - 1] != Path.AltDirectorySeparatorChar)
        {
            path += Path.DirectorySeparatorChar;
        }
        return path;
    }

    #endregion

    #region 相对路径

    /// <summary>
    /// 获取 targetPath 相对于 basePath 的相对路径。
    /// 使用统一的大小写处理（不区分系统大小写），适合跨平台和 Web 地址场景。
    /// </summary>
    /// <param name="basePath">基准路径</param>
    /// <param name="targetPath">目标路径</param>
    /// <returns>相对路径，如果无法获取（如不同盘符）则返回原路径</returns>
    public static string GetRelativePath(string basePath, string targetPath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(targetPath))
            return targetPath ?? string.Empty;

        basePath = Normalize(Path.GetFullPath(basePath));
        targetPath = Normalize(Path.GetFullPath(targetPath));

        // 不同盘符无法求相对路径
        if (basePath.Length >= 2 && targetPath.Length >= 2
            && basePath[1] == ':' && targetPath[1] == ':'
            && char.ToUpperInvariant(basePath[0]) != char.ToUpperInvariant(targetPath[0]))
        {
            return targetPath;
        }

        basePath = EnsureTrailingSeparator(basePath);

        Uri baseUri = new Uri(basePath);
        Uri targetUri = new Uri(targetPath);
        Uri relativeUri = baseUri.MakeRelativeUri(targetUri);

        string relative = Uri.UnescapeDataString(relativeUri.ToString());
        return Normalize(relative);
    }

    #endregion

    #region 路径安全检查

    /// <summary>
    /// 安全解析路径：先相对路径拼接到根目录，再检查是否越界。
    /// 如果路径尝试逃逸出根目录，返回 null。
    /// </summary>
    /// <param name="path">待校验的路径（可为相对路径）</param>
    /// <param name="rootDirectory">信任的根目录</param>
    /// <returns>在根目录内的完整路径，越界返回 null</returns>
    public static string SafeResolve(string path, string rootDirectory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootDirectory))
            return null;

        try
        {
            rootDirectory = Path.GetFullPath(Normalize(rootDirectory));
            string fullPath = Path.GetFullPath(Path.Combine(rootDirectory, path));
            fullPath = Normalize(fullPath);

            string root = EnsureTrailingSeparator(rootDirectory);
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }
        }
        catch
        {
            // 路径不合法
        }

        return null;
    }

    /// <summary>
    /// 检查 childPath 是否在 parentPath 目录之下（含子目录）。
    /// </summary>
    /// <param name="parentPath">父目录路径</param>
    /// <param name="childPath">子路径</param>
    /// <returns>是否为子路径</returns>
    public static bool IsSubPath(string parentPath, string childPath)
    {
        if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(childPath))
            return false;

        try
        {
            parentPath = Path.GetFullPath(Normalize(parentPath));
            childPath = Path.GetFullPath(Normalize(childPath));

            string parent = EnsureTrailingSeparator(parentPath);
            return childPath.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查给定路径是否包含路径遍历特征（如 ".."）。
    /// 注意：这只是文本级检查，实际安全校验请使用 SafeResolve。
    /// </summary>
    /// <param name="path">待检查路径</param>
    /// <returns>是否包含可疑内容</returns>
    public static bool ContainsTraversal(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // 检查 ".." 和非法字符
        string[] parts = path.Split('/', '\\');
        foreach (string part in parts)
        {
            if (part == ".." || part == ".")
                return true;
            if (part.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return true;
        }
        return false;
    }

    #endregion

    #region 文件名处理

    /// <summary>
    /// 过滤文件名中的非法字符（替换为 "_"），同时去除前后空白。
    /// 不会截断过长的文件名，Unity/Windows 限制约 260 字符。
    /// </summary>
    /// <param name="fileName">原始文件名</param>
    /// <returns>合法的文件名</returns>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = fileName.Trim();

        foreach (char c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // 移除开头和结尾的点号（Windows 不允许文件夹名为 '.' 结尾）
        sanitized = sanitized.Trim('.', ' ');

        if (string.IsNullOrEmpty(sanitized))
            sanitized = "__unnamed__";

        return sanitized;
    }

    /// <summary>
    /// 检查文件名是否合法（不含非法字符、非空）。
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>是否合法</returns>
    public static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        return true;
    }

    /// <summary>
    /// 获取文件扩展名（不含点号）。如 "data.json" 返回 "json"。
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>扩展名（不含点号），无扩展名返回空字符串</returns>
    public static string GetExtensionWithoutDot(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        string ext = Path.GetExtension(path);
        return ext.StartsWith(".") ? ext.Substring(1) : ext;
    }

    /// <summary>
    /// 更改文件路径的扩展名。
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="newExtension">新扩展名（可带或不带点）</param>
    /// <returns>新路径</returns>
    public static string ChangeExtension(string path, string newExtension)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        if (string.IsNullOrEmpty(newExtension))
            return Path.GetFileNameWithoutExtension(path);

        if (!newExtension.StartsWith("."))
            newExtension = "." + newExtension;

        return Path.ChangeExtension(path, newExtension);
    }

    #endregion

    #region 路径信息

    /// <summary>
    /// 获取路径相对于给定根目录的深度层级。
    /// 如 "A/B/C" 相对于 "A" 深度为 2。
    /// </summary>
    /// <param name="path">目标路径</param>
    /// <param name="rootPath">根目录</param>
    /// <returns>深度，越界返回 -1</returns>
    public static int GetDepth(string path, string rootPath)
    {
        string relative = GetRelativePath(rootPath, path);
        if (relative == path)
            return -1;

        string[] parts = Normalize(relative).Split(
            new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length;
    }

    /// <summary>
    /// 获取路径的父目录。等价于 Path.GetDirectoryName，但保证不以分隔符结尾。
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>父目录路径</returns>
    public static string GetParentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        string parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent))
            return string.Empty;

        return Normalize(parent);
    }

    /// <summary>
    /// 判断路径是否为绝对路径。
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>是否为绝对路径</returns>
    public static bool IsAbsolutePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return Path.IsPathRooted(path);
    }

    /// <summary>
    /// 判断路径是否以 Unity "Assets/" 为前缀（编辑器资源路径）。
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>是否以 "Assets/" 开头</returns>
    public static bool IsUnityAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalized = UseForwardSlash(path);
        return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 实用方法

    /// <summary>
    /// 获取一个唯一的文件路径：如果目标文件已存在，自动追加序号后缀。
    /// 如 "result.txt" → "result (1).txt" → "result (2).txt" ... 直到不冲突为止。
    /// </summary>
    /// <param name="filePath">期望的文件路径</param>
    /// <returns>不冲突的唯一文件路径</returns>
    public static string GetUniqueFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        if (!File.Exists(filePath))
            return filePath;

        string dir = Path.GetDirectoryName(filePath);
        string name = Path.GetFileNameWithoutExtension(filePath);
        string ext = Path.GetExtension(filePath);
        int index = 1;

        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name} ({index}){ext}");
            index++;
        } while (File.Exists(newPath) && index < int.MaxValue);

        return newPath;
    }

    /// <summary>
    /// 检查路径是否有指定的扩展名（不区分大小写）。
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="extension">扩展名（带或不带点号均可）</param>
    /// <returns>是否匹配</returns>
    public static bool HasExtension(string path, string extension)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(extension))
            return false;

        if (!extension.StartsWith("."))
            extension = "." + extension;

        return Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断路径是否指向目录（而非文件）。
    /// 不靠谱的启发式方法：没有扩展名 + 不以点结尾 = 可能是目录。
    /// 确切判断请用 Directory.Exists。
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>估计是否为目录路径</returns>
    public static bool LooksLikeDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalized = Normalize(path);

        // 以分隔符结尾 → 目录
        if (normalized.EndsWith(Path.DirectorySeparatorChar.ToString()))
            return true;

        // 无扩展名 → 可能是目录
        string ext = Path.GetExtension(normalized);
        if (string.IsNullOrEmpty(ext))
            return true;

        return false;
    }

    /// <summary>
    /// 将 Assets-relative 路径（如 "Assets/Prefabs/Enemy.prefab"）转换为文件系统绝对路径。
    /// </summary>
    /// <param name="assetPath">以 "Assets/" 开头的资源路径</param>
    /// <returns>文件系统绝对路径</returns>
    public static string AssetPathToAbsolute(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return string.Empty;

        string normalized = UseForwardSlash(assetPath);

        if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            string relative = normalized.Substring("Assets/".Length);
            return Combine(Application.dataPath, relative);
        }

        return assetPath;
    }

    /// <summary>
    /// 将文件系统绝对路径转换为 Assets-relative 路径。
    /// </summary>
    /// <param name="absolutePath">文件系统绝对路径</param>
    /// <returns>如 "Assets/Prefabs/Enemy.prefab"，不在 Assets 下返回原路径</returns>
    public static string AbsoluteToAssetPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return string.Empty;

        string normalized = Normalize(absolutePath);
        string dataPath = Normalize(Application.dataPath);

        if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            string relative = normalized.Substring(dataPath.Length).TrimStart(Path.DirectorySeparatorChar);
            return "Assets/" + UseForwardSlash(relative);
        }

        return absolutePath;
    }

    /// <summary>
    /// 将 Resources.Load 路径转换为完整资源路径（不带扩展名）。
    /// 如 "UI/Panel/MainPanel" → "Assets/Resources/UI/Panel/MainPanel.prefab"（推测）
    /// </summary>
    /// <param name="resourcePath">Resources 相对路径（不带扩展名）</param>
    /// <returns>Assets 路径</returns>
    public static string ResourcesPathToAssetPath(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return string.Empty;

        return UseForwardSlash(Combine("Assets", "Resources", resourcePath));
    }

    #endregion
}