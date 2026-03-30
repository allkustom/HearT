using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class tutorialSoundExample : MonoBehaviour
{
    [Range(0, 1)]
    public int mode = 0;

    public RawImage blurImage;
    public RawImage midImage;
    public RawImage clearImage;

    private float currentDistance;
    private bool isInside20Percent = false;

    private SymptomManager symptomManager;

    private static List<tutorialSoundExample> instances = new List<tutorialSoundExample>();

    private void Awake()
    {
        symptomManager = GetComponent<SymptomManager>();
    }

    private void OnEnable()
    {
        if (!instances.Contains(this))
            instances.Add(this);
    }

    private void OnDisable()
    {
        if (instances.Contains(this))
            instances.Remove(this);
    }

    public void ApplyByDistance(float distance, float range)
    {
        currentDistance = distance;

        if (midImage != null)
        {
            float alpha = (range <= 0f) ? 1f : 1f - Mathf.Clamp01(distance / range);
            SetImageAlpha(midImage, alpha);
        }

        float triggerDistance = range * 0.8f;
        isInside20Percent = distance <= triggerDistance;

        if (mode == 0)
        {
            // 버튼 입력으로만 clearImage 켜짐
        }
        else if (mode == 1)
        {
            if (clearImage == null) return;

            if (isInside20Percent && !clearImage.gameObject.activeSelf)
            {
                clearImage.gameObject.SetActive(true);
                CheckTutorialCompletion();
            }
        }
    }

    public void TriggerClearImage()
    {
        if (clearImage == null) return;

        clearImage.gameObject.SetActive(true);
        CheckTutorialCompletion();
    }

    public void HideClearImage()
    {
        if (clearImage == null) return;
        clearImage.gameObject.SetActive(false);
    }

    public bool CanTriggerByButton()
    {
        if (mode != 0) return false;
        if (!isInside20Percent) return false;
        if (clearImage == null) return false;
        if (clearImage.gameObject.activeSelf) return false;
        if (symptomManager == null) return false;
        if (Esp32SppSerialReceiver.Instance == null) return false;

        // SymptomManager.type 와 현재 ESP32 type이 같아야 함
        if (symptomManager.type != Esp32SppSerialReceiver.Instance.type) return false;

        return true;
    }

    public static bool TriggerMode0ByButton()
    {
        tutorialSoundExample bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (var item in instances)
        {
            if (item == null) continue;

            if (!item.CanTriggerByButton())
            {
                item.DebugWhySkipped();
                continue;
            }

            if (item.currentDistance < bestDistance)
            {
                bestDistance = item.currentDistance;
                bestTarget = item;
            }
        }

        if (bestTarget != null)
        {
            bestTarget.TriggerClearImage();
            Debug.Log($"Triggered clearImage on: {bestTarget.gameObject.name}");
            return true;
        }

        Debug.Log("No valid mode 0 tutorial target found.");
        return false;
    }

    private void DebugWhySkipped()
    {
        float requiredDistance = -1f;
        if (symptomManager != null)
        {
            requiredDistance = symptomManager.range * 0.2f;
        }

        string reason = $"{gameObject.name} skipped -> ";

        if (mode != 0) reason += "mode != 0 / ";

        if (!isInside20Percent)
        {
            reason += $"not inside 20% (current:{currentDistance:F3}, need<={requiredDistance:F3}) / ";
        }

        if (clearImage == null) reason += "clearImage null / ";
        else if (clearImage.gameObject.activeSelf) reason += "already cleared / ";

        if (symptomManager == null) reason += "SymptomManager missing / ";

        if (Esp32SppSerialReceiver.Instance == null) reason += "SPP receiver missing / ";
        else if (symptomManager != null && symptomManager.type != Esp32SppSerialReceiver.Instance.type)
        {
            reason += $"type mismatch (obj:{symptomManager.type}, esp:{Esp32SppSerialReceiver.Instance.type}) / ";
        }

        Debug.Log(reason);
    }

    private static void CheckTutorialCompletion()
    {
        if (instances.Count == 0) return;

        foreach (var item in instances)
        {
            if (item == null) return;
            if (item.clearImage == null) return;
            if (!item.clearImage.gameObject.activeSelf) return;
        }

        if (TotalUIManager.Instance != null)
        {
            TotalUIManager.Instance.isTutorialInteract = false;
            TotalUIManager.Instance.enableClickProceed = true;
            Debug.Log("All tutorial items completed.");
        }
    }

    private void SetImageAlpha(RawImage image, float alpha)
    {
        if (image == null) return;

        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    void Start()
    {
        if (clearImage != null)
            clearImage.gameObject.SetActive(false);

        if (midImage != null)
            midImage.gameObject.SetActive(true);

        if (blurImage != null)
            blurImage.gameObject.SetActive(true);

        if (midImage != null)
            SetImageAlpha(midImage, 0f);
    }
}