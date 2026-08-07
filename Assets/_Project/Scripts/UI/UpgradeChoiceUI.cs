using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Overdraft.Core;
using Overdraft.Generation;

namespace Overdraft.UI
{
    /// <summary>
    /// The M2 upgrade choice screen (FR-471). Deliberately hand-rolled input polling instead of
    /// uGUI's EventSystem/Selectable navigation stack — this project has exactly one interactive
    /// menu screen so far, and correctly wiring InputSystemUIInputModule's action references is
    /// its own project; polling devices directly (same pattern RunController already uses for
    /// the restart prompt) is simpler and cannot silently fail to respond to input.
    /// <para>Functional and ugly, per the M2-T09 brief. The real UI pass is M3/M4.</para>
    /// </summary>
    public sealed class UpgradeChoiceUI : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] GameObject root;
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform[] cardRects;
        [SerializeField] TMP_Text[] cardTexts;
        [SerializeField] Image[] cardHighlights;

        int selectedIndex;
        int offerCount;
        Action<int> onConfirmed;

        void Awake()
        {
            if (root != null) { root.SetActive(false); }
        }

        public void Show(List<UpgradeOffer> offers, Action<int> confirmed)
        {
            offerCount = Mathf.Min(offers.Count, cardTexts.Length);
            onConfirmed = confirmed;
            selectedIndex = 0;

            for (int i = 0; i < cardTexts.Length; i++)
            {
                bool active = i < offerCount;
                cardTexts[i].gameObject.SetActive(active);
                if (active) { cardTexts[i].SetText(Describe(tuning, offers[i])); }
            }

            UpdateHighlight();
            root.SetActive(true);
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        void Update()
        {
            if (!root.activeSelf || offerCount == 0) { return; }

            if (ConsumePrevious()) { Move(-1); }
            if (ConsumeNext()) { Move(1); }

            int clicked = ConsumeClickedCard();
            if (clicked >= 0)
            {
                selectedIndex = clicked;
                Confirm();
                return;
            }

            if (ConsumeConfirm()) { Confirm(); }
        }

        void Confirm()
        {
            int chosen = selectedIndex;
            Action<int> callback = onConfirmed;
            Hide();
            callback?.Invoke(chosen);
        }

        void Move(int delta)
        {
            selectedIndex = ((selectedIndex + delta) % offerCount + offerCount) % offerCount;
            UpdateHighlight();
        }

        void UpdateHighlight()
        {
            // Ion, not Bank gold — gold is reserved for CHARGE alone (docs/04 §1).
            for (int i = 0; i < cardHighlights.Length; i++)
            {
                if (cardHighlights[i] == null) { continue; }
                cardHighlights[i].color = i == selectedIndex ? Palette.Ion : Palette.Grid;
            }
        }

        int ConsumeClickedCard()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) { return -1; }

            Vector2 point = Mouse.current.position.ReadValue();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            for (int i = 0; i < offerCount; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(cardRects[i], point, cam))
                {
                    return i;
                }
            }
            return -1;
        }

        static bool ConsumePrevious()
        {
            bool k = Keyboard.current != null &&
                (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame);
            bool g = Gamepad.current != null &&
                (Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.left.wasPressedThisFrame);
            return k || g;
        }

        static bool ConsumeNext()
        {
            bool k = Keyboard.current != null &&
                (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame);
            bool g = Gamepad.current != null &&
                (Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame);
            return k || g;
        }

        static bool ConsumeConfirm()
        {
            bool k = Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
            bool g = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            return k || g;
        }

        static string Describe(TuningConfig t, in UpgradeOffer offer)
        {
            switch (offer.Line)
            {
                case UpgradeLine.Spread:
                    if (offer.Tier == 1) { return $"SPREAD I\n{t.Upgrades.spreadT1Projectiles} shots, {t.Upgrades.spreadT1ConeDegrees:0}° cone"; }
                    if (offer.Tier == 2) { return $"SPREAD II\n{t.Upgrades.spreadT2Projectiles} shots, {t.Upgrades.spreadT2ConeDegrees:0}° cone"; }
                    return $"SPREAD III\n{t.Upgrades.spreadT3Projectiles} shots, {t.Upgrades.spreadT3ConeDegrees:0}° cone, {t.Upgrades.spreadT3DamageMultiplier:0.00}x dmg";

                case UpgradeLine.Volatile:
                    if (offer.Tier == 1) { return $"VOLATILE I\nExplode on kill, {t.Upgrades.volatileT1Radius:0.0}m radius"; }
                    if (offer.Tier == 2) { return $"VOLATILE II\nBigger blast, chains once"; }
                    return "VOLATILE III\nChains freely";

                case UpgradeLine.Leech:
                    if (offer.Tier == 1) { return $"LEECH I\nBank in {t.Bank.leechT1Duration:0.00}s"; }
                    if (offer.Tier == 2) { return $"LEECH II\nBank in {t.Bank.leechT2Duration:0.00}s\nDash refunds progress"; }
                    return $"LEECH III\nBank in {t.Bank.leechT3Duration:0.00}s\nI-frames on completion";

                case UpgradeLine.Greed:
                    if (offer.Tier == 1) { return "GREED I\nFaster MULT gain, faster decay"; }
                    if (offer.Tier == 2) { return "GREED II\nEven faster, no MULT cap"; }
                    return "GREED III\n1.5x enemy value\nFull charge loss on hit";

                default:
                    return offer.Line + " " + offer.Tier;
            }
        }
    }
}
