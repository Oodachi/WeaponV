using System;
using Bannerlord.UIExtenderEx;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace WeaponV;

// UI
[PrefabExtension("Inventory", "descendant::Widget[@Id='RightEquipmentList']/Children/InventoryEquippedItemSlot")]
public class InventoryExtensionPatch : PrefabExtensionInsertPatch {
	public override InsertType Type => InsertType.Append;
	
	[PrefabExtensionFileName]
	public string PatchFileName => "WeaponV.xml";
}

// UI数据
[ViewModelMixin]
public class SpInventoryVMMixin : BaseViewModelMixin<SPInventoryVM> {
	private HintViewModel _equipmentWeaponSlotHint = new();
	
	private SPItemVM _characterWeapon5Slot = new();
	
	private static Func<SPItemVM> _getCharacterWeapon5SlotAction = () => new SPItemVM();
	
	private static Action<SPItemVM> _setCharacterWeapon5SlotAction = _ => { };
	
	private readonly Harmony _harmony = new("WeaponV.SpInventoryVMMixin");
	
	public SpInventoryVMMixin(SPInventoryVM vm) : base(vm) {
		EquipmentWeapon5SlotHint = new HintViewModel(new TextObject("{=WeaponV2}Additional weapons"));
		
		_setCharacterWeapon5SlotAction = SetCharacterWeapon5Slot;
		_getCharacterWeapon5SlotAction = GetCharacterWeapon5Slot;
		
		var traverse  = Traverse.Create(vm);
		var equipment = traverse.Method("get_ActiveEquipment").GetValue<Equipment>();
		var itemSlots = Traverse.Create(equipment).Field<EquipmentElement[]>("_itemSlots").Value;
		if (itemSlots.Length < 13) {
			var newItemSlots = new EquipmentElement[13];
			for (var index = 0; index < itemSlots.Length; ++index) { newItemSlots[index] = new EquipmentElement(equipment[index]); }
			
			Traverse.Create(equipment).Field("_itemSlots").SetValue(newItemSlots);
		}
		
		var itemRosterElement = new ItemRosterElement(equipment.GetEquipmentFromSlot((EquipmentIndex)12), 1);
		
		CharacterWeapon5Slot = traverse.Method("InitializeCharacterEquipmentSlot", itemRosterElement, (EquipmentIndex)12).GetValue<SPItemVM>();
		
		_harmony.Patch(
			AccessTools.Method(typeof(InventoryLogic), "TransferIsMovementValid"),
			postfix: new HarmonyMethod(AccessTools.Method(typeof(SpInventoryVMMixin), nameof(TransferIsMovementValidPostfix))));
		
		_harmony.Patch(
			AccessTools.Method(typeof(SPInventoryVM), "IsItemEquipmentPossible"),
			postfix: new HarmonyMethod(AccessTools.Method(typeof(SpInventoryVMMixin), nameof(IsItemEquipmentPossiblePostfix))));
		
		_harmony.Patch(
			AccessTools.Method(typeof(SPInventoryVM), "GetItemFromIndex"),
			postfix: new HarmonyMethod(AccessTools.Method(typeof(SpInventoryVMMixin), nameof(GetItemFromIndexPostfix))));
		
		_harmony.Patch(
			AccessTools.Method(typeof(SPInventoryVM), "RefreshEquipment"),
			postfix: new HarmonyMethod(AccessTools.Method(typeof(SpInventoryVMMixin), nameof(RefreshEquipmentPostfix))));
		
		_harmony.Patch(
			AccessTools.Method(typeof(SPInventoryVM), "UpdateCharacterEquipment"),
			postfix: new HarmonyMethod(AccessTools.Method(typeof(SpInventoryVMMixin), nameof(UpdateCharacterEquipmentPostfix))));
	}
	
	public override void OnFinalize() {
		base.OnFinalize();
		_harmony.UnpatchAll();
	}
	
	public override void OnRefresh() {
		base.OnRefresh();
		CharacterWeapon5Slot.RefreshValues();
	}
	
	private void SetCharacterWeapon5Slot(SPItemVM vm) {
		CharacterWeapon5Slot = vm;
	}
	
	private SPItemVM GetCharacterWeapon5Slot() {
		return CharacterWeapon5Slot;
	}
	
	// 更新角色装备
	private static void UpdateCharacterEquipmentPostfix(SPInventoryVM __instance) {
		var traverse          = Traverse.Create(__instance);
		var equipment         = traverse.Method("get_ActiveEquipment").GetValue<Equipment>();
		var itemRosterElement = new ItemRosterElement(equipment.GetEquipmentFromSlot((EquipmentIndex)12), 1);
		
		var characterWeapon5Slot = traverse.Method("InitializeCharacterEquipmentSlot", itemRosterElement, (EquipmentIndex)12).GetValue<SPItemVM>();
		_setCharacterWeapon5SlotAction(characterWeapon5Slot);
	}
	
	// 判断转移是否有效
	private static void TransferIsMovementValidPostfix(InventoryLogic __instance, TransferCommand transferCommand, ref bool __result) {
		if (transferCommand.ToEquipmentIndex.GetHashCode() != 12) return;
		
		var inventoryItemTypeOfItem = InventoryManager.GetInventoryItemTypeOfItem(transferCommand.ElementToTransfer.EquipmentElement.Item);
		
		if (inventoryItemTypeOfItem != InventoryItemType.Weapon) return;
		__result = true;
	}
	
	// 判断物品是否可以装备到对应的槽位
	private static void IsItemEquipmentPossiblePostfix(SPInventoryVM __instance, SPItemVM itemVM, ref bool __result) {
		if (__instance.TargetEquipmentIndex == 12) __result                                      = true;
		if (itemVM.ItemType.GetHashCode() == 12 && __instance.TargetEquipmentIndex < 4) __result = true;
	}
	
	// 根据装备索引获取装备
	private static void GetItemFromIndexPostfix(SPInventoryVM __instance, EquipmentIndex itemType, ref SPItemVM __result) {
		if (itemType.GetHashCode() != 12) return;
		__result = _getCharacterWeapon5SlotAction.Invoke();
	}
	
	// 刷新装备 正常显示装备图标
	private static void RefreshEquipmentPostfix(SPInventoryVM __instance, SPItemVM itemVM, EquipmentIndex itemType) {
		if (itemType.GetHashCode() == 12) _getCharacterWeapon5SlotAction.Invoke().RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
	}
	
	[DataSourceProperty]
	public SPItemVM CharacterWeapon5Slot
	{
		get => _characterWeapon5Slot;
		set
		{
			if (value == _characterWeapon5Slot)
				return;
			_characterWeapon5Slot = value;
			OnPropertyChangedWithValue(value);
		}
	}
	
	[DataSourceProperty]
	public HintViewModel EquipmentWeapon5SlotHint
	{
		get => _equipmentWeaponSlotHint;
		set
		{
			if (value == _equipmentWeaponSlotHint)
				return;
			_equipmentWeaponSlotHint = value;
			OnPropertyChangedWithValue(value);
		}
	}
}

// 战斗场景切换武器的逻辑
[HarmonyPatch]
public class AgentPatch {
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Agent), "WieldNextWeapon")]
	private static void WieldNextWeaponPostfix(Agent __instance, Agent.HandIndex weaponIndex) {
		// 副手不进行替换
		if (weaponIndex != Agent.HandIndex.MainHand) return;
		
		// 非英雄不进行替换
		if (!__instance.IsHero) return;
		
		// 战斗设备
		var missionEquipment = __instance.Equipment;
		
		// 武器槽5为空的时候不需要执行替换逻辑
		var missionWeapon = missionEquipment[5];
		if (missionWeapon.IsEmpty) return;
		
		// 有空槽位直接进行切换
		for (var index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.NumPrimaryWeaponSlots; ++index) {
			if (!missionEquipment[index].IsEmpty) continue;
			__instance.EquipWeaponWithNewEntity(index, ref missionWeapon);
			missionEquipment[5] = MissionWeapon.Invalid;
			return;
		}
		
		// 空手切装备的时候不进行替换, 会数组越界
		var mainHandIndex = __instance.GetWieldedItemIndex(Agent.HandIndex.MainHand);
		if (mainHandIndex == EquipmentIndex.None) return;
		
		// 0,1,2,3,5槽位里只有一个主手武器的时候不进行替换
		if (missionWeapon.IsAnyAmmo() || missionWeapon.IsShield()) {
			var mainHandWeaponCount = 0;
			for (var index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.NumPrimaryWeaponSlots; ++index) {
				if (missionEquipment[index].IsAnyAmmo() || missionEquipment[index].IsShield()) { continue; }
				
				mainHandWeaponCount += 1;
			}
			
			if (mainHandWeaponCount < 2) { return; }
		}
		
		// 主手武器槽位
		var mainHandWeapon = missionEquipment[mainHandIndex];
		__instance.EquipWeaponWithNewEntity(mainHandIndex, ref missionWeapon);
		missionEquipment[5] = mainHandWeapon;
	}
}

// 战斗场景生成Agent是战斗设备
[HarmonyPatch]
public class MissionEquipmentPatch {
	[HarmonyPostfix]
	[HarmonyPatch(typeof(MissionEquipment), MethodType.Constructor)]
	private static void NoAgeConstructorPostfix(MissionEquipment __instance) {
		Traverse.Create(__instance).Field<MissionWeapon[]>("_weaponSlots").Value = new MissionWeapon[6];
	}
	
	[HarmonyPostfix]
	[HarmonyPatch(typeof(MissionEquipment), MethodType.Constructor, typeof(Equipment), typeof(Banner))]
	private static void MissionEquipmentConstructorPostfix(MissionEquipment __instance, Equipment spawnEquipment, Banner banner) {
		var equipmentElement = spawnEquipment[12];
		if (equipmentElement.IsEmpty) return;
		MissionWeapon[] weaponSlots   = Traverse.Create(__instance).Field<MissionWeapon[]>("_weaponSlots").Value;
		var             itemObject    = equipmentElement.Item;
		var             itemModifier  = equipmentElement.ItemModifier;
		MissionWeapon   missionWeapon = new MissionWeapon(itemObject, itemModifier, banner);
		weaponSlots[5] = missionWeapon;
	}
}

[HarmonyPatch]
public class EquipmentPatch {
	// 修改构造函数把12槽位加出来
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Equipment), MethodType.Constructor)]
	private static void NoAgeConstructorPostfix(Equipment __instance) {
		Traverse.Create(__instance).Field<EquipmentElement[]>("_itemSlots").Value = new EquipmentElement[13];
	}
	
	// 修改构造函数把12槽位加出来
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Equipment), MethodType.Constructor, typeof(bool))]
	private static void IsCivilianConstructorPostfix(Equipment __instance, bool isCivilian) {
		Traverse.Create(__instance).Field<EquipmentElement[]>("_itemSlots").Value = new EquipmentElement[13];
	}
	
	// 修改构造函数把12槽位加出来
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Equipment), MethodType.Constructor, typeof(Equipment))]
	private static void EquipmentConstructorPostfix(Equipment __instance, Equipment equipment) {
		var itemSlots    = Traverse.Create(__instance).Field<EquipmentElement[]>("_itemSlots").Value;
		var newItemSlots = new EquipmentElement[13];
		for (var index = 0; index < itemSlots.Length; ++index) { newItemSlots[index] = new EquipmentElement(equipment[index]); }
		
		Traverse.Create(__instance).Field("_itemSlots").SetValue(newItemSlots);
	}
	
	// 复制装备的时候保证复制扩展出来的12槽
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Equipment), "Clone")]
	private static void EquipmentClonePostfix(Equipment __instance, ref Equipment __result, bool cloneWithoutWeapons = false) {
		if (cloneWithoutWeapons) return;
		var itemSlots = Traverse.Create(__instance).Field<EquipmentElement[]>("_itemSlots").Value;
		if (itemSlots.Length != 13) return;
		__result[12] = __instance[12];
	}
}

public class SubModule : MBSubModuleBase {
	private UIExtender _extender = null!;
	
	private Harmony _harmony = null!;
	
	protected override void OnSubModuleLoad() {
		base.OnSubModuleLoad();
		_extender = new UIExtender("WeaponV");
		_extender.Register(typeof(SubModule).Assembly);
		_extender.Enable();
		
		_harmony = new Harmony("WeaponV");
		_harmony.PatchAll();
	}
	
	protected override void OnSubModuleUnloaded() {
		base.OnSubModuleUnloaded();
		_extender.Disable();
		_harmony.UnpatchAll();
	}
	
	protected override void OnBeforeInitialModuleScreenSetAsRoot() {
		base.OnBeforeInitialModuleScreenSetAsRoot();
		InformationManager.DisplayMessage(
			new InformationMessage(new TextObject("{=WeaponV1}WeaponV loaded successfully!").ToString(), Colors.Green));
	}
}