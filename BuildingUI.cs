using System.Collections.Generic;
using System.Linq;
using Core;
using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace BuildingSystem
{
    public class BuildingUI : SingletonGObj<BuildingUI>
    {
        [SerializeField] private Transform parentInputSlot;
        [SerializeField] private InventorySlot inputPrefab;
        [SerializeField] private InventorySlot outputSlot;
        [SerializeField] private GameObject panelUI;
        [SerializeField] private Image progressBar;

        private List<InventorySlot> _currentInputSlots = new List<InventorySlot>();
        public List<InventorySlot> CurrentInputSlots => _currentInputSlots;
        public InventorySlot OutputSlot => outputSlot;

        private void OnSlotsUpdated() => BuildingManager.Instance.OnUpdateUI();

        public void SetProgress(float progress) => progressBar.fillAmount = progress;

        public void ShowUI() => panelUI.SetActive(true);

        public void HideUI() => panelUI.SetActive(false);

        public void SpawnInputSlots(int maxInputCount, List<ItemRequirement> currentInputItems, ItemRequirement currentOutputItem)
        {
            ClearInputSlots();

            for (int i = 0; i < maxInputCount; i++)
            {
                InventorySlot slot = Instantiate(inputPrefab, parentInputSlot);
                _currentInputSlots.Add(slot);

                if (i < currentInputItems.Count && currentInputItems[i]?.item != null)
                    InventoryManager.Instance.SpawnNewItemAt(slot, currentInputItems[i].item, currentInputItems[i].count);

                slot.onItemUpdate += OnSlotsUpdated;
            }

            if (currentOutputItem != null)
                InventoryManager.Instance.SpawnNewItemAt(outputSlot, currentOutputItem.item, currentOutputItem.count);

            outputSlot.onItemUpdate += OnSlotsUpdated;
        }

        private void ClearInputSlots()
        {
            outputSlot.onItemUpdate -= OnSlotsUpdated;
            foreach (var slot in _currentInputSlots)
            {
                if (slot != null)
                {
                    slot.onItemUpdate -= OnSlotsUpdated;
                    Destroy(slot.gameObject);
                }
            }
            _currentInputSlots.Clear();
            outputSlot.DestroyItem();
        }
    }
}