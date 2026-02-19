using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;

[BepInPlugin("com.ash.liveload", "Ash's live loader", "1.2.1")]
public class liveload : BaseUnityPlugin
{
    private string wpath;
    private FileSystemWatcher watcher;

    private readonly Dictionary<string, List<GameObject>> loadedMods =
        new Dictionary<string, List<GameObject>>();

    private readonly Queue<Action> mainThreadQueue =
        new Queue<Action>();

    void Awake()
    {
        wpath = Path.Combine(Paths.BepInExRootPath, "LIVEMODSHERE!");
        if (!Directory.Exists(wpath))
            Directory.CreateDirectory(wpath);

        setwat();
        Logger.LogInfo("loader ready. Drop your mods in bepinex/LIVEMODSHERE! to load/unload the uh mods.");
    }

    void Update()
    {
        while (mainThreadQueue.Count > 0)
        {
            mainThreadQueue.Dequeue().Invoke();
        }
    }

    void setwat()
    {
        watcher = new FileSystemWatcher(wpath, "*.dll");

        watcher.Created += (s, e) =>
        {
            System.Threading.Thread.Sleep(500);
            lassembly(e.FullPath);
        };

        watcher.Deleted += (s, e) =>
        {
            ulassembly(e.FullPath);
        };

        watcher.Renamed += (s, e) =>
        {
            ulassembly(e.OldFullPath);
            System.Threading.Thread.Sleep(200);
            lassembly(e.FullPath);
        };

        watcher.EnableRaisingEvents = true;
    }

    void lassembly(string path)
    {
        try
        {
            if (!File.Exists(path)) return;

            byte[] dllData = File.ReadAllBytes(path);
            Assembly assembly = Assembly.Load(dllData);

            Type[] typesToLoad = assembly.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract)
                .ToArray();

            List<GameObject> spawnedObjects = new List<GameObject>();

            foreach (Type type in typesToLoad)
            {
                mainThreadQueue.Enqueue(delegate
                {
                    GameObject modRoot = new GameObject("Live_" + assembly.GetName().Name + "_" + type.Name);
                    modRoot.AddComponent(type);
                    spawnedObjects.Add(modRoot);

                    Logger.LogInfo("Loaded: " + type.Name);
                });
            }

            loadedMods[path] = spawnedObjects;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error loading " + Path.GetFileName(path) + ": " + ex.Message);
        }
    }

    void ulassembly(string path)
    {
        List<GameObject> objects;

        if (!loadedMods.TryGetValue(path, out objects))
            return;

        mainThreadQueue.Enqueue(delegate
        {
            foreach (GameObject obj in objects)
            {
                if (obj != null)
                    Destroy(obj);
            }

            loadedMods.Remove(path);
            Logger.LogInfo("unloaded mod from " + Path.GetFileName(path));
        });
    }
}
