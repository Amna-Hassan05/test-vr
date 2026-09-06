using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Singleton CSV logger for AuthXR behavioral auth demos.
/// One file per Play session under ProjectRoot/Data (Editor / PC).
/// Console mirroring works even if the CSV file fails to open.
/// </summary>
public class VRInteractionLogger : MonoBehaviour
{
    public static VRInteractionLogger Instance { get; private set; }

    [Tooltip("Also print each logged row to the Unity Console.")]
    public bool mirrorToConsole = true;

    [Tooltip("Optional file name prefix. Final name includes session timestamp.")]
    public string filePrefix = "authxr_session";

    [Tooltip("Folder name under the Unity project root (next to Assets).")]
    public string projectDataFolderName = "Data";

    StreamWriter writer;
    string filePath;
    readonly object writeLock = new object();
    bool headerWritten;

    public string LogFilePath => filePath;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        mirrorToConsole = true;
        DontDestroyOnLoad(gameObject);
        OpenSessionFile();

        try
        {
            if (GetComponent<AuthXRRuntimeBootstrap>() == null)
                gameObject.AddComponent<AuthXRRuntimeBootstrap>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VRInteractionLogger] AuthXRRuntimeBootstrap failed (logging still active): {e.Message}");
        }

        Debug.Log("[VRInteractionLogger] Ready. Live console mirroring is ON.");
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        CloseSessionFile();
    }

    void OnApplicationQuit()
    {
        CloseSessionFile();
    }

    static string ResolveLogDirectory(string folderName)
    {
#if UNITY_EDITOR
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, folderName);
#elif UNITY_STANDALONE_WIN
        string exeDir = Path.GetDirectoryName(Application.dataPath);
        string candidate = Path.Combine(exeDir ?? ".", folderName);
        try
        {
            Directory.CreateDirectory(candidate);
            return candidate;
        }
        catch
        {
            return Application.persistentDataPath;
        }
#else
        return Application.persistentDataPath;
#endif
    }

    void OpenSessionFile()
    {
        try
        {
            string dir = ResolveLogDirectory(projectDataFolderName);
            Directory.CreateDirectory(dir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            filePath = Path.Combine(dir, $"{filePrefix}_{stamp}.csv");

            writer = new StreamWriter(filePath, append: false) { AutoFlush = true };
            writer.WriteLine(
                "timestamp_unix_ms,timestamp_unity,event_type,object_name,controller_name,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w,speed_mps,interactor_name");
            headerWritten = true;

            Debug.Log($"[VRInteractionLogger] Session CSV: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VRInteractionLogger] Failed to open log file: {e.Message}");
            writer = null;
            headerWritten = false;
        }
    }

    void CloseSessionFile()
    {
        lock (writeLock)
        {
            if (writer == null)
                return;

            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRInteractionLogger] Error closing log: {e.Message}");
            }
            finally
            {
                writer = null;
            }
        }
    }

    public void LogRow(
        string eventType,
        string objectName = "",
        string controllerName = "",
        Vector3? position = null,
        Quaternion? rotation = null,
        float? speed = null,
        string interactorName = "")
    {
        Vector3 p = position ?? Vector3.zero;
        Quaternion r = rotation ?? Quaternion.identity;
        float spd = speed ?? 0f;

        long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        float unityT = Time.time;

        string row = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1:F4},{2},{3},{4},{5:F6},{6:F6},{7:F6},{8:F6},{9:F6},{10:F6},{11:F6},{12:F6},{13}",
            unixMs,
            unityT,
            Csv(eventType),
            Csv(objectName),
            Csv(controllerName),
            p.x, p.y, p.z,
            r.x, r.y, r.z, r.w,
            spd,
            Csv(interactorName));

        // Console first — never blocked by CSV file issues.
        if (mirrorToConsole)
            Debug.Log($"[VRInteractionLogger] {row}");

        if (writer == null || !headerWritten)
            return;

        lock (writeLock)
        {
            try
            {
                writer.WriteLine(row);
                writer.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRInteractionLogger] Write failed: {e.Message}");
            }
        }
    }

    static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
