using System.Collections.Generic;
using System.Linq;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CircuitSuperstars;

namespace IntervalDisplay;

public enum DisplayType {
    IntervalToLeader,
    Interval,
	AutoCycle,
	ManualCycle
}

class CheckpointManager {
	private static Dictionary<RacingTeamID, List<float>> checkpointTimes = new Dictionary<RacingTeamID, List<float>>();
	public static Dictionary<RacingTeamID, IntervalDisplay> intervalDisplays = new Dictionary<RacingTeamID, IntervalDisplay>();
	public static bool isTracking = false;
	
	[HarmonyPatch(typeof(CircuitSuperstars.RacerProgressManager), nameof(CircuitSuperstars.RacerProgressManager.BeginTrackingProgress))]
	[HarmonyPostfix]
	static void Initialize() {
		checkpointTimes.Clear();
		intervalDisplays.Clear();
		isTracking = true;
	}

	[HarmonyPatch(typeof(CircuitSuperstars.RacerProgressManager), nameof(CircuitSuperstars.RacerProgressManager.StopTrackingProgress))]
	[HarmonyPostfix]
	static void Deinitialize() {
		isTracking = false;
	}

	[HarmonyPatch(typeof(CircuitSuperstars.RacerProgressManager), nameof(CircuitSuperstars.RacerProgressManager.OnCheckpointPassed))]
	[HarmonyPostfix]
	static void TrackCheckpoints(CircuitSuperstars.RacerProgressManager __instance, AutomobilePipeline automobile) {
		if (!isTracking) return;

		RacingTeamID id =  automobile.RacingTeamId;

		if (!checkpointTimes.ContainsKey(id)) {
			checkpointTimes[id] = new List<float>();
		}

		int num = __instance.racingTeamIds.IndexOf(id);
		int checkpoint = __instance.lapAdjustedCheckpoints[num];
		float time = RaceManager.Instance.TimeSinceRaceStart;

		while (checkpointTimes[id].Count<=checkpoint) {
			checkpointTimes[id].Add(time);
		}

		if (intervalDisplays.ContainsKey(id)) {
			intervalDisplays[id].SetTimeDifference(GetIntervalToAhead(id,Plugin.IsIntervalToNearest()));
		}
	}

	public static float GetIntervalBetween(RacingTeamID behinde, RacingTeamID ahead) {
		if (checkpointTimes[behinde].Count==0 && checkpointTimes[ahead].Count>0) {
			return checkpointTimes[ahead].Last();
		} else if (checkpointTimes[behinde].Count>0 && checkpointTimes[ahead].Count==0) {
			return -checkpointTimes[behinde].Last();
		} else if (checkpointTimes[behinde].Count==0 && checkpointTimes[ahead].Count==0) {
			return 0;
		}

		int commonLap = 0;
		if (checkpointTimes[behinde].Count>=checkpointTimes[ahead].Count) {
			commonLap = checkpointTimes[ahead].Count-1;
		} else {
			commonLap = checkpointTimes[behinde].Count-1;
		}

		return checkpointTimes[behinde][commonLap]-checkpointTimes[ahead][commonLap];
    }

	public static float GetIntervalToAhead(RacingTeamID id, bool nearest=true) {
		float diff = TimeFormat.InvalidTime;

		foreach (RacingTeamID ahead in checkpointTimes.Keys) {
			if (id==ahead) continue;

			float tmpDiff = GetIntervalBetween(id, ahead);
			if (tmpDiff<0) continue;

			if (diff==TimeFormat.InvalidTime || (nearest && diff>tmpDiff) || (!nearest && diff<tmpDiff)) {
				diff = tmpDiff;
			}
		}

		return diff;
	}

	public static void UpdateAll() {
		foreach (RacingTeamID id in intervalDisplays.Keys) {
			intervalDisplays[id].SetTimeDifference(GetIntervalToAhead(id,Plugin.IsIntervalToNearest()));
		}
	}
}

public class IntervalDisplay : MonoBehaviour {
    private HUDStandingEntry entry;
    private TextMeshProUGUI intervalText;
	private Image intervalBackground;

	private static Color DEFAULT_BACKGROUND_COLOR = new Color(0.0314f, 0f, 0.1608f, 0.749f);

    void Awake() {
        entry = GetComponent<HUDStandingEntry>();
        CreateTimeDiffElement();
    }

    void CreateTimeDiffElement() {
        GameObject bg = new GameObject("intervalDisplayBackground");
        bg.transform.SetParent(transform, false);
        RectTransform bgRect = bg.AddComponent<RectTransform>();

        bgRect.anchorMin = new Vector2(1, 0.5f);
        bgRect.anchorMax = new Vector2(1, 0.5f);
        bgRect.pivot = new Vector2(0, 0.5f);
        bgRect.anchoredPosition = new Vector2(20, 0);
        bgRect.sizeDelta = new Vector2(100, 40);

		intervalBackground = bg.AddComponent<Image>();
		intervalBackground.color = new Color(0,0,0,0);

        GameObject display = new GameObject("intervalDisplay");
        display.transform.SetParent(transform, false);
        RectTransform displayRect = display.AddComponent<RectTransform>();

        displayRect.anchorMin = new Vector2(1, 0.5f);
        displayRect.anchorMax = new Vector2(1, 0.5f);
        displayRect.pivot = new Vector2(0, 0.5f);
        displayRect.anchoredPosition = new Vector2(25, 0);
        displayRect.sizeDelta = new Vector2(80, 40);

        intervalText = display.AddComponent<TextMeshProUGUI>();
		intervalText.font = entry.nameUpdatableText.textDisplay.font;
        intervalText.fontSize = entry.nameUpdatableText.textDisplay.fontSize;
        intervalText.alignment = TextAlignmentOptions.MidlineLeft;
        intervalText.color = entry.nameUpdatableText.textDisplay.color;
        intervalText.text = "";
    }

    public void SetTimeDifference(float diff) {
        if (entry.FinishedRace || entry.UsingPitlane) {
            intervalText.text = "";
			intervalBackground.color = new Color(0,0,0,0);
            return;
        }
        if (diff==TimeFormat.InvalidTime || diff<=0f) {
            intervalText.text = "";
			intervalBackground.color = new Color(0,0,0,0);
            return;
        }
        string formatted = $"+{TimeFormat.SecondsMilliseconds(diff)}";
        intervalText.text = formatted;
		intervalBackground.color = DEFAULT_BACKGROUND_COLOR;
    }
}

class StandingEntryExtender {
	[HarmonyPatch(typeof(CircuitSuperstars.HUDDisplayConnector), "UpdateStandingsEntry")]
	[HarmonyPostfix]
	static void UpdateStandingsEntry(CircuitSuperstars.HUDDisplayConnector __instance, HUDStandingEntry se, int standing, RacingTeamID racingTeamID) {
		if (se==null) return;

		if (standing==1) {
			if (CheckpointManager.intervalDisplays.ContainsKey(racingTeamID)) {
				CheckpointManager.intervalDisplays.Remove(racingTeamID);
			}
		} else {
			IntervalDisplay ext = se.GetComponent<IntervalDisplay>();
			if (ext==null) ext = se.gameObject.AddComponent<IntervalDisplay>();
			CheckpointManager.intervalDisplays[racingTeamID] = ext;

			ext.SetTimeDifference(CheckpointManager.GetIntervalToAhead(racingTeamID,Plugin.IsIntervalToNearest()));
		}
	}
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

	internal static ConfigEntry<DisplayType> displayType;
	internal static ConfigEntry<float> cycleInterval;
	internal static ConfigEntry<KeyboardShortcut> cycleButton;

	private static bool isIntervalToNearest;
	private static float lastTimeChanged = 0;
        
	private void BindConfig() {
		displayType = Config.Bind<DisplayType>(
			"General",
			"displayType",
			DisplayType.Interval
		);

		cycleInterval = Config.Bind<float>(
			"General",
			"cycleInterval",
			10,
			new ConfigDescription("Time between switching display type", new AcceptableValueRange<float>(0,60))
		);

		cycleButton = Config.Bind<KeyboardShortcut>(
			"General",
			"cycleBytton",
			new KeyboardShortcut(KeyCode.G),
			"Button that switches display type"
		);

		isIntervalToNearest = displayType.Value==DisplayType.IntervalToLeader;
	}

	public static bool IsIntervalToNearest() {
		switch (displayType.Value) {
			case DisplayType.IntervalToLeader: return false;
			case DisplayType.Interval: return true;
			default: return isIntervalToNearest;
		}
	}

    private void Awake() {
        Logger = base.Logger;

		BindConfig();

		var instance = new Harmony(MyPluginInfo.PLUGIN_GUID);
		instance.PatchAll(typeof(CheckpointManager));
		instance.PatchAll(typeof(StandingEntryExtender));
    }

	private void Update() {
		if (!CheckpointManager.isTracking) {
			lastTimeChanged = -1;
			return;
		}

		float time = RaceManager.Instance.TimeSinceRaceStart;

		if (cycleButton.Value.IsDown()) {
			isIntervalToNearest = !isIntervalToNearest;
			lastTimeChanged = time;
			CheckpointManager.UpdateAll();
		} else if (displayType.Value==DisplayType.AutoCycle) {

			if (lastTimeChanged<0) lastTimeChanged = time;

			if (time-lastTimeChanged<cycleInterval.Value) return;

			lastTimeChanged = time;
			isIntervalToNearest = !isIntervalToNearest;

			CheckpointManager.UpdateAll();
		}
    }
}
