using System.Collections.Generic;
using UnityEngine;

/* ════════════════════════════════════════════════════════════════════
 *  AudioManager.cs — 全局音效/BGM 管理器（★新增文件）
 * ════════════════════════════════════════════════════════════════════
 *
 *  【Unity 里怎么配置？只做一次】
 *   1. 在「开始界面」场景新建空物体，命名为 AudioManager
 *   2. 把本脚本挂上去
 *   3. Inspector 里把音效 wav 全部拖进 SfxClips，BGM 拖进 BgmClips
 *      （顺序随意，按文件名自动识别）
 *   4. 完成。脚本自带 DontDestroyOnLoad，切场景后依然生效，
 *      其他场景不需要再建
 *
 *  【代码里怎么用？】
 *      AudioManager.PlaySfx("move");      // 播音效（文件名即音效名）
 *      AudioManager.PlaySfx("win", 0.8f); // 第二个参数可选，调节音量
 *      AudioManager.PlayBgm("lobby");     // 播BGM并循环
 *      AudioManager.StopBgm();            // 停BGM
 *
 *  【随机变体】
 *      同一个音效想有多种变体（避免重复音呆板）：
 *      文件命名为  move_1.wav / move_2.wav / move_3.wav ...
 *      代码照旧调  AudioManager.PlaySfx("move");
 *      每次播放会从所有变体里随机挑一个。
 *      只放一个 move.wav（不带数字）也完全兼容。
 *
 *  【安全性】
 *      场景里没放 AudioManager 时，所有调用自动静默跳过，不报错。
 *      BGM 同名已在播时不会重启（切场景来回不炸）。
 * ════════════════════════════════════════════════════════════════════ */
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效列表：全部拖进来(文件名=音效名，支持 xxx_1/xxx_2 变体)")]
    public AudioClip[] SfxClips;

    [Header("BGM列表：2个BGM wav拖进来(bgm_lobby / bgm_game)")]
    public AudioClip[] BgmClips;

    [Header("音量调节")]
    [Range(0f, 1f)] public float SfxVolume = 1f;
    [Range(0f, 1f)] public float BgmVolume = 0.6f;

    // 音效索引：基础名 → 变体列表（"move_1/move_2" 都归到 "move" 名下）
    private readonly Dictionary<string, List<AudioClip>> sfxMap = new Dictionary<string, List<AudioClip>>();
    private readonly Dictionary<string, AudioClip> bgmMap = new Dictionary<string, AudioClip>();
    private AudioSource bgmSource;
    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private const int SfxSourceCount = 6;   // 允许6个音效同时响

    private void Awake()
    {
        // 单例 + 跨场景存活
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 建立音效名字索引（AudioClip.name 不含扩展名）
        if (SfxClips != null)
            foreach (var c in SfxClips)
            {
                if (c == null) continue;
                string baseName = GetSfxBaseName(c.name);
                if (!sfxMap.TryGetValue(baseName, out var list))
                {
                    list = new List<AudioClip>();
                    sfxMap[baseName] = list;
                }
                list.Add(c);
            }

        // BGM 索引（BGM 不需要变体）
        if (BgmClips != null)
            foreach (var c in BgmClips)
                if (c != null) bgmMap[c.name] = c;

        // BGM 专用音源（循环播放）
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = BgmVolume;

        // 音效音源池（多个音效可叠放，如 落子+将军 连响）
        for (int i = 0; i < SfxSourceCount; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            sfxSources.Add(s);
        }
    }

    /// <summary>
    /// 解析音效基础名："move_2" → "move"；没有"_数字"后缀则原样返回。
    /// 注意 "countdown_last" 这类下划线后不是纯数字的名字不受影响。
    /// </summary>
    private static string GetSfxBaseName(string clipName)
    {
        int idx = clipName.LastIndexOf('_');
        if (idx > 0 && idx < clipName.Length - 1)
        {
            bool allDigits = true;
            for (int i = idx + 1; i < clipName.Length; i++)
                if (!char.IsDigit(clipName[i])) { allDigits = false; break; }
            if (allDigits) return clipName.Substring(0, idx);
        }
        return clipName;
    }

    /// <summary>播放音效。用法：AudioManager.PlaySfx("move")。有多个变体(xxx_1/xxx_2...)时随机选一个。</summary>
    public static void PlaySfx(string name, float volume = 1f)
    {
        if (Instance == null) return;   // 场景里没放 AudioManager → 静默跳过
        if (!Instance.sfxMap.TryGetValue(name, out var list) || list.Count == 0) return;

        // 随机挑一个变体（只有一个时等价于固定播它）
        AudioClip clip = list.Count == 1 ? list[0] : list[Random.Range(0, list.Count)];

        // 找一个空闲音源，全忙就用第一个叠加
        AudioSource src = Instance.sfxSources.Find(s => !s.isPlaying);
        if (src == null) src = Instance.sfxSources[0];
        src.PlayOneShot(clip, volume * Instance.SfxVolume);
    }

    /// <summary>播放BGM并循环。同名BGM已在播则不重启。用法：AudioManager.PlayBgm("lobby")</summary>
    public static void PlayBgm(string name)
    {
        if (Instance == null) return;
        if (!Instance.bgmMap.TryGetValue(name, out var clip)) return;
        if (Instance.bgmSource.clip == clip && Instance.bgmSource.isPlaying) return;

        Instance.bgmSource.clip = clip;
        Instance.bgmSource.volume = Instance.BgmVolume;
        Instance.bgmSource.Play();
    }

    /// <summary>停止BGM</summary>
    public static void StopBgm()
    {
        if (Instance == null) return;
        Instance.bgmSource.Stop();
    }
}
