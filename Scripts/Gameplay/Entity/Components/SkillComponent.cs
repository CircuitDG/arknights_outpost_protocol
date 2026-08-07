using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Doctor;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Managers;
using System;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Entity.Components;

/// <summary>
/// 技能组件（数据驱动完整版）
/// 职责：技能数据加载、冷却管理、施法流程、效果执行
/// </summary>
public partial class SkillComponent : Node
{
    // ============================================================
    // 导出变量
    // ============================================================

    [ExportGroup("技能配置")]
    [Export] public int SkillCount = 4; // F1-F4

    // ============================================================
    // 运行时状态
    // ============================================================

    private BaseEntity _owner;
    private Dictionary<int, string> _skillSlots = new(); // Slot → SkillId
    private Dictionary<string, SkillData> _skillData = new();
    private Dictionary<string, float> _cooldowns = new();
    private Dictionary<string, float> _castTimers = new();
    private Dictionary<string, bool> _isCasting = new();
    private bool _skillsLoadPending = true;

    // ============================================================
    // 事件
    // ============================================================

    public event Action<int, SkillData> SkillCasted;
    public event Action<int, float> CooldownUpdated; // Slot, Progress (0-1)
    public event Action<int, bool> CastStateChanged; // Slot, IsCasting

    // ============================================================
    // 公共属性
    // ============================================================

    public bool IsAnyCasting
    {
        get
        {
            foreach (var kvp in _isCasting)
            {
                if (kvp.Value) return true;
            }
            return false;
        }
    }

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        _owner = GetParent<BaseEntity>();
        if (_owner == null)
        {
            GD.PushError("[SkillComponent] 必须挂载在 BaseEntity 下");
            return;
        }

        TryLoadSkillsFromData();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 数据就绪后补加载（DataManager 异步加载）
        if (_skillsLoadPending)
        {
            TryLoadSkillsFromData();
        }

        // 更新所有技能的冷却
        var keys = new List<string>(_cooldowns.Keys);
        foreach (var key in keys)
        {
            if (_cooldowns.TryGetValue(key, out float cd) && cd > 0)
            {
                _cooldowns[key] = Math.Max(0, cd - dt);
                UpdateSlotCooldown(key);
            }
        }

        // 更新施法计时器
        var castKeys = new List<string>(_castTimers.Keys);
        foreach (var key in castKeys)
        {
            if (_castTimers.TryGetValue(key, out float ct) && ct > 0)
            {
                _castTimers[key] = Math.Max(0, ct - dt);
                if (_castTimers[key] <= 0 && _isCasting.TryGetValue(key, out bool casting) && casting)
                {
                    // 施法完成，执行技能效果
                    ExecuteSkillEffect(key);
                    _isCasting[key] = false;
                    CastStateChanged?.Invoke(GetSlotForSkill(key), false);
                }
            }
        }
    }

    // ============================================================
    // 技能加载
    // ============================================================

    private void TryLoadSkillsFromData()
    {
        if (!_skillsLoadPending) return;
        if (DataManager.Instance == null || !DataManager.Instance.IsLoaded) return;
        if (_owner is not Operator op || op.Data == null) return;

        _skillsLoadPending = false;

        foreach (var binding in op.Data.Skills)
        {
            if (binding.Slot < 1 || binding.Slot > SkillCount) continue;

            var skill = DataManager.Instance.GetSkill(binding.SkillId);
            if (skill == null)
            {
                GD.PushWarning($"[SkillComponent] 未找到技能: {binding.SkillId} for {op.EntityName}");
                continue;
            }

            _skillSlots[binding.Slot] = binding.SkillId;
            _skillData[binding.SkillId] = skill;
            _cooldowns[binding.SkillId] = 0;
            _castTimers[binding.SkillId] = 0;
            _isCasting[binding.SkillId] = false;

            GD.Print($"[SkillComponent] {op.EntityName} 已加载技能: F{binding.Slot} → {skill.Name}");
        }
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>获取指定槽位的技能数据</summary>
    public SkillData GetSkill(int slot)
    {
        if (!_skillSlots.TryGetValue(slot, out string skillId)) return null;
        return _skillData.GetValueOrDefault(skillId);
    }

    /// <summary>获取指定槽位的技能冷却进度 (0-1)</summary>
    public float GetCooldownProgress(int slot)
    {
        if (!_skillSlots.TryGetValue(slot, out string skillId)) return 0f;
        if (!_skillData.TryGetValue(skillId, out var skill)) return 0f;
        if (!_cooldowns.TryGetValue(skillId, out float cd)) return 0f;

        return skill.Cooldown > 0 ? cd / skill.Cooldown : 0f;
    }

    /// <summary>获取指定槽位的技能是否可用</summary>
    public bool IsSkillReady(int slot)
    {
        if (!_skillSlots.TryGetValue(slot, out string skillId)) return false;
        if (!_skillData.TryGetValue(skillId, out _)) return false;
        if (!_cooldowns.TryGetValue(skillId, out float cd)) return false;
        if (_isCasting.TryGetValue(skillId, out bool casting) && casting) return false;

        return cd <= 0;
    }

    /// <summary>获取指定槽位的技能是否正在施法</summary>
    public bool IsCasting(int slot)
    {
        if (!_skillSlots.TryGetValue(slot, out string skillId)) return false;
        return _isCasting.TryGetValue(skillId, out bool casting) && casting;
    }

    /// <summary>释放指定槽位的技能（体力消耗由调用方 Doctor 处理）</summary>
    public bool CastSkill(int slot)
    {
        if (!_skillSlots.TryGetValue(slot, out string skillId))
        {
            GD.Print($"[SkillComponent] 槽位 {slot} 未绑定技能");
            return false;
        }

        if (!_skillData.TryGetValue(skillId, out var skill))
        {
            GD.Print($"[SkillComponent] 技能数据不存在: {skillId}");
            return false;
        }

        if (_cooldowns.TryGetValue(skillId, out float cd) && cd > 0)
        {
            GD.Print($"[SkillComponent] 技能 {skill.Name} 冷却中 ({cd:F1}s)");
            return false;
        }

        if (_isCasting.TryGetValue(skillId, out bool casting) && casting)
        {
            GD.Print($"[SkillComponent] 技能 {skill.Name} 正在施法中");
            return false;
        }

        // 开始施法
        _isCasting[skillId] = true;
        _castTimers[skillId] = skill.CastTime;

        GD.Print($"[SkillComponent] {_owner.EntityName} 开始施法: {skill.Name} ({skill.CastTime}s)");
        CastStateChanged?.Invoke(slot, true);
        TriggerCastEffects(skill);

        // 施法时间为 0 时立即执行
        if (skill.CastTime <= 0)
        {
            ExecuteSkillEffect(skillId);
            _isCasting[skillId] = false;
            CastStateChanged?.Invoke(slot, false);
        }

        return true;
    }

    // ============================================================
    // 技能效果执行
    // ============================================================

    private void ExecuteSkillEffect(string skillId)
    {
        if (!_skillData.TryGetValue(skillId, out var skill)) return;

        GD.Print($"[SkillComponent] {_owner.EntityName} 释放技能: {skill.Name}");

        // 进入冷却
        _cooldowns[skillId] = skill.Cooldown;
        UpdateSlotCooldown(skillId);

        // 根据效果类型执行
        switch (skill.EffectType)
        {
            case "Buff":
                ExecuteBuffEffect(skill);
                break;
            case "Damage":
                ExecuteDamageEffect(skill);
                break;
            case "Heal":
                ExecuteHealEffect(skill);
                break;
            case "Summon":
                ExecuteSummonEffect(skill);
                break;
            default:
                GD.PushWarning($"[SkillComponent] 未知效果类型: {skill.EffectType}");
                break;
        }

        // 广播技能释放事件
        int slot = GetSlotForSkill(skillId);
        SkillCasted?.Invoke(slot, skill);
        EventBus.Instance.EmitSkillCast(slot, skillId, _owner);

        TriggerEffectVFX(skill);
    }

    private void ExecuteBuffEffect(SkillData skill)
    {
        var targets = GetTargets(skill);
        float duration = skill.EffectParams.GetValueOrDefault("duration", 3.0f);

        foreach (var target in targets)
        {
            if (target == null || target.IsDead) continue;

            // 攻速 Buff
            if (skill.EffectParams.TryGetValue("attackSpeedBonus", out float speedBonus) && speedBonus > 0)
            {
                BuffManager.Instance.AddBuff(target, BuffType.AttackSpeed, speedBonus, duration, sourceSkillId: skill.Id);
                GD.Print($"[SkillComponent] {target.EntityName} 攻速 +{speedBonus * 100}%");
            }

            // 攻击力 Buff
            if (skill.EffectParams.TryGetValue("attackBonus", out float attackBonus) && attackBonus > 0)
            {
                BuffManager.Instance.AddBuff(target, BuffType.Attack, attackBonus, duration, sourceSkillId: skill.Id);
                GD.Print($"[SkillComponent] {target.EntityName} 攻击力 +{attackBonus * 100}%");
            }

            // 防御 Buff
            if (skill.EffectParams.TryGetValue("defenseBonus", out float defBonus) && defBonus > 0)
            {
                BuffManager.Instance.AddBuff(target, BuffType.Defense, defBonus, duration, sourceSkillId: skill.Id);
                GD.Print($"[SkillComponent] {target.EntityName} 防御 +{defBonus * 100}%");
            }
        }
    }

    private void ExecuteDamageEffect(SkillData skill)
    {
        var targets = GetTargets(skill);

        foreach (var target in targets)
        {
            if (target == null || target.IsDead) continue;

            float multiplier = skill.EffectParams.GetValueOrDefault("damageMultiplier", 1.0f);
            if (_owner is Operator caster && caster.Data?.ClassType == "Caster")
            {
                multiplier *= (1f + CollectionManager.CasterSkillDamageBonus);
            }
            int baseDamage = _owner is Operator op ? (op.Attack?.AttackDamage ?? 20) : 20;
            int damage = (int)(baseDamage * multiplier);

            target.TakeDamage(damage, _owner);
            GD.Print($"[SkillComponent] {_owner.EntityName} 对 {target.EntityName} 造成 {damage} 点伤害 (x{multiplier})");

            if (skill.EffectParams.TryGetValue("stunDuration", out float stun) && stun > 0)
            {
                BuffManager.Instance.AddBuff(target, BuffType.Stun, 0f, stun, sourceSkillId: skill.Id);
                GD.Print($"[SkillComponent] {target.EntityName} 被眩晕 {stun}s");
            }
        }
    }

    private void ExecuteHealEffect(SkillData skill)
    {
        var targets = GetTargets(skill);
        float healPercent = skill.EffectParams.GetValueOrDefault("healPercent", 0.3f);

        foreach (var target in targets)
        {
            if (target == null || target.IsDead || target is not Operator op || op.Health == null) continue;

            int healAmount = (int)(op.Health.MaxHealth * healPercent);
            op.Heal(healAmount);
            GD.Print($"[SkillComponent] {_owner.EntityName} 治疗 {op.EntityName} +{healAmount} HP");
        }

        // 范围治疗额外覆盖博士
        if (skill.TargetType == "Area")
        {
            float range = skill.EffectParams.GetValueOrDefault("range", skill.TargetRange);
            var doctor = GetTree().GetFirstNodeInGroup("doctor") as Node2D;
            if (doctor is Doctor doctorNode && !doctorNode.IsDead &&
                _owner.GlobalPosition.DistanceTo(doctorNode.GlobalPosition) <= range)
            {
                float healAmount = doctorNode.MaxHealthValue * healPercent;
                doctorNode.Heal(healAmount);
                GD.Print($"[SkillComponent] {_owner.EntityName} 治疗博士 +{healAmount:F0} HP");
            }
        }
    }

    private void ExecuteSummonEffect(SkillData skill)
    {
        // TODO: 召唤物（如无人机）
        GD.Print("[SkillComponent] 召唤效果待实现");
    }

    // ============================================================
    // 目标选择
    // ============================================================

    private List<BaseEntity> GetTargets(SkillData skill)
    {
        var result = new List<BaseEntity>();

        switch (skill.TargetType)
        {
            case "Self":
                result.Add(_owner);
                break;

            case "Target":
                if (_owner is Operator op && op.Attack != null)
                {
                    var target = op.Attack.CurrentTarget;
                    if (target != null && !target.IsDead)
                    {
                        result.Add(target);
                    }
                }
                break;

            case "Area":
                foreach (var entity in GetAllEntitiesInRange(skill.TargetRange))
                {
                    if (entity.Faction == _owner.Faction && !entity.IsDead)
                    {
                        result.Add(entity);
                    }
                }
                break;

            default:
                result.Add(_owner);
                break;
        }

        return result;
    }

    private List<BaseEntity> GetAllEntitiesInRange(float range)
    {
        var space = _owner.GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D();
        query.Shape = new CircleShape2D { Radius = range };
        query.Transform = new Transform2D(0, _owner.GlobalPosition);
        query.CollisionMask = (1u << 1) | (1u << 2); // 干员 + 敌人

        var results = space.IntersectShape(query);
        var entities = new List<BaseEntity>();

        foreach (var result in results)
        {
            var collider = result["collider"].As<GodotObject>();
            if (collider is BaseEntity entity)
            {
                entities.Add(entity);
            }
        }

        return entities;
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private int GetSlotForSkill(string skillId)
    {
        foreach (var kvp in _skillSlots)
        {
            if (kvp.Value == skillId) return kvp.Key;
        }
        return -1;
    }

    private void UpdateSlotCooldown(string skillId)
    {
        int slot = GetSlotForSkill(skillId);
        if (slot <= 0) return;

        float progress = GetCooldownProgress(slot);
        CooldownUpdated?.Invoke(slot, progress);
        EventBus.Instance.EmitSkillCooldownUpdated(slot, progress);
    }

    private void TriggerCastEffects(SkillData skill)
    {
        // TODO: 动画系统
        if (!string.IsNullOrEmpty(skill.AnimationTrigger))
        {
            // 通知动画系统（占位）
        }
    }

    private void TriggerEffectVFX(SkillData skill)
    {
        if (!string.IsNullOrEmpty(skill.VfxPath))
        {
            GD.Print($"[SkillComponent] VFX: {skill.VfxPath}");
        }

        if (!string.IsNullOrEmpty(skill.SfxPath))
        {
            GD.Print($"[SkillComponent] SFX: {skill.SfxPath}");
        }
    }
}
