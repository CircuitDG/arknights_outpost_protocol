using Godot;
using OutpostProtocol.Core.EventBus;
using OutpostProtocol.Data;
using OutpostProtocol.Gameplay.Character.Operator;
using OutpostProtocol.Gameplay.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OutpostProtocol.Managers;

/// <summary>
/// Buff 管理器（AutoLoad 单例）
/// 职责：统一管理所有活跃 Buff，定时更新，到期自动还原
/// </summary>
public partial class BuffManager : Node
{
    // ============================================================
    // 单例
    // ============================================================

    private static BuffManager _instance;
    public static BuffManager Instance => _instance;

    // ============================================================
    // 运行时数据
    // ============================================================

    private readonly List<BuffData> _activeBuffs = new();
    private readonly List<BuffData> _pendingRemoval = new();

    // ============================================================
    // 公共属性
    // ============================================================

    public IReadOnlyList<BuffData> ActiveBuffs => _activeBuffs;

    // ============================================================
    // 生命周期
    // ============================================================

    public override void _Ready()
    {
        if (_instance != null)
        {
            GD.PushWarning("BuffManager 已存在，销毁重复实例");
            QueueFree();
            return;
        }
        _instance = this;
        GD.Print("[BuffManager] 初始化完成");
    }

    public override void _ExitTree()
    {
        _instance = null;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        _pendingRemoval.Clear();

        foreach (var buff in _activeBuffs)
        {
            buff.RemainingTime -= dt;
            buff.OnTick?.Invoke(buff.Target, dt);

            if (buff.IsExpired)
            {
                _pendingRemoval.Add(buff);
            }
        }

        foreach (var buff in _pendingRemoval)
        {
            RemoveBuff(buff);
        }
    }

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>添加 Buff</summary>
    public BuffData AddBuff(
        BaseEntity target,
        BuffType type,
        float value,
        float duration,
        Action<BaseEntity> onApply = null,
        Action<BaseEntity> onRemove = null,
        Action<BaseEntity, float> onTick = null,
        string sourceSkillId = null)
    {
        if (target == null || target.IsDead)
        {
            GD.PushWarning("[BuffManager] 目标无效，无法添加 Buff");
            return null;
        }

        var buff = new BuffData
        {
            Target = target,
            Type = type,
            Value = value,
            Duration = duration,
            RemainingTime = duration,
            OnApply = onApply,
            OnRemove = onRemove,
            OnTick = onTick,
            SourceSkillId = sourceSkillId,
        };

        SaveOriginalValue(buff);
        ApplyBuffEffect(buff);
        onApply?.Invoke(target);

        _activeBuffs.Add(buff);

        GD.Print($"[BuffManager] 添加 Buff: {type} +{value * 100}% 持续 {duration:F1}s 目标 {target.EntityName}");
        EventBus.Instance.EmitLogMessage($"Buff 生效: {type} +{value * 100}% 持续 {duration:F1}s", "INFO");
        EventBus.Instance.EmitBuffApplied(buff.Id, type, target, duration);

        return buff;
    }

    /// <summary>移除指定 Buff（先出列再还原，避免自己计入剩余叠加）</summary>
    public bool RemoveBuff(BuffData buff)
    {
        if (buff == null || !_activeBuffs.Contains(buff)) return false;

        _activeBuffs.Remove(buff);
        RemoveBuffEffect(buff);
        buff.OnRemove?.Invoke(buff.Target);

        GD.Print($"[BuffManager] 移除 Buff: {buff.Type} 从 {buff.Target?.EntityName ?? "null"}");
        if (buff.Target != null)
        {
            EventBus.Instance.EmitBuffRemoved(buff.Id, buff.Type, buff.Target);
        }

        return true;
    }

    /// <summary>移除目标身上的所有 Buff</summary>
    public void RemoveAllBuffs(BaseEntity target)
    {
        var toRemove = _activeBuffs.Where(b => b.Target == target).ToList();
        foreach (var buff in toRemove)
        {
            RemoveBuff(buff);
        }
    }

    /// <summary>移除目标身上指定类型的 Buff</summary>
    public void RemoveBuffsOfType(BaseEntity target, BuffType type)
    {
        var toRemove = _activeBuffs.Where(b => b.Target == target && b.Type == type).ToList();
        foreach (var buff in toRemove)
        {
            RemoveBuff(buff);
        }
    }

    /// <summary>检查目标是否拥有指定类型的 Buff</summary>
    public bool HasBuff(BaseEntity target, BuffType type)
    {
        return _activeBuffs.Any(b => b.Target == target && b.Type == type);
    }

    /// <summary>获取目标上指定类型的 Buff 总值（百分比加和）</summary>
    public float GetBuffValue(BaseEntity target, BuffType type)
    {
        return _activeBuffs
            .Where(b => b.Target == target && b.Type == type)
            .Sum(b => b.Value);
    }

    // ============================================================
    // 核心方法
    // ============================================================

    private void SaveOriginalValue(BuffData buff)
    {
        switch (buff.Type)
        {
            case BuffType.Attack:
                if (buff.Target is Operator op && op.Attack != null)
                    buff.OriginalValue = op.Attack.AttackDamage;
                break;
            case BuffType.AttackSpeed:
                if (buff.Target is Operator op2 && op2.Attack != null)
                    buff.OriginalValue = op2.Attack.AttackInterval;
                break;
            case BuffType.MoveSpeed:
                if (buff.Target?.Movement != null)
                    buff.OriginalValue = buff.Target.Movement.Speed;
                break;
            // Defense/HealBonus/Stun/Shield 暂不需要原始值
        }
    }

    private void ApplyBuffEffect(BuffData buff)
    {
        if (buff.Target == null || buff.Target.IsDead) return;

        switch (buff.Type)
        {
            case BuffType.Attack: ApplyAttackBuff(buff); break;
            case BuffType.AttackSpeed: ApplyAttackSpeedBuff(buff); break;
            case BuffType.Defense: ApplyDefenseBuff(buff); break;
            case BuffType.HealBonus: ApplyHealBonusBuff(buff); break;
            case BuffType.MoveSpeed: ApplyMoveSpeedBuff(buff); break;
            case BuffType.Stun: ApplyStunBuff(buff); break;
            case BuffType.Shield: ApplyShieldBuff(buff); break;
        }
    }

    private void RemoveBuffEffect(BuffData buff)
    {
        if (buff.Target == null || buff.Target.IsDead) return;

        switch (buff.Type)
        {
            case BuffType.Attack: RemoveAttackBuff(buff); break;
            case BuffType.AttackSpeed: RemoveAttackSpeedBuff(buff); break;
            case BuffType.Defense: RemoveDefenseBuff(buff); break;
            case BuffType.HealBonus: RemoveHealBonusBuff(buff); break;
            case BuffType.MoveSpeed: RemoveMoveSpeedBuff(buff); break;
            case BuffType.Stun: RemoveStunBuff(buff); break;
            case BuffType.Shield: RemoveShieldBuff(buff); break;
        }
    }

    // ============================================================
    // Attack Buff（百分比加和叠加）
    // ============================================================

    private void ApplyAttackBuff(BuffData buff)
    {
        if (buff.Target is not Operator op || op.Attack == null) return;

        int original = (int)(buff.OriginalValue ?? op.Attack.AttackDamage);
        float total = GetBuffValue(buff.Target, BuffType.Attack) + buff.Value;
        op.Attack.AttackDamage = (int)(original * (1 + total));
    }

    private void RemoveAttackBuff(BuffData buff)
    {
        if (buff.Target is not Operator op || op.Attack == null) return;

        int original = (int)(buff.OriginalValue ?? op.Attack.AttackDamage);
        float total = GetBuffValue(buff.Target, BuffType.Attack);
        op.Attack.AttackDamage = total > 0 ? (int)(original * (1 + total)) : original;
    }

    // ============================================================
    // AttackSpeed Buff
    // ============================================================

    private void ApplyAttackSpeedBuff(BuffData buff)
    {
        if (buff.Target is not Operator op || op.Attack == null) return;

        float original = (float)(buff.OriginalValue ?? op.Attack.AttackInterval);
        float total = GetBuffValue(buff.Target, BuffType.AttackSpeed) + buff.Value;
        op.Attack.AttackInterval = original / (1 + total);
    }

    private void RemoveAttackSpeedBuff(BuffData buff)
    {
        if (buff.Target is not Operator op || op.Attack == null) return;

        float original = (float)(buff.OriginalValue ?? op.Attack.AttackInterval);
        float total = GetBuffValue(buff.Target, BuffType.AttackSpeed);
        op.Attack.AttackInterval = total > 0 ? original / (1 + total) : original;
    }

    // ============================================================
    // MoveSpeed Buff
    // ============================================================

    private void ApplyMoveSpeedBuff(BuffData buff)
    {
        if (buff.Target?.Movement == null) return;

        float original = (float)(buff.OriginalValue ?? buff.Target.Movement.Speed);
        float total = GetBuffValue(buff.Target, BuffType.MoveSpeed) + buff.Value;
        buff.Target.Movement.Speed = original * (1 + total);
    }

    private void RemoveMoveSpeedBuff(BuffData buff)
    {
        if (buff.Target?.Movement == null) return;

        float original = (float)(buff.OriginalValue ?? buff.Target.Movement.Speed);
        float total = GetBuffValue(buff.Target, BuffType.MoveSpeed);
        buff.Target.Movement.Speed = total > 0 ? original * (1 + total) : original;
    }

    // ============================================================
    // Defense / HealBonus / Stun / Shield（占位）
    // ============================================================

    private void ApplyDefenseBuff(BuffData buff)
    {
        GD.Print($"[BuffManager] 防御 Buff: +{buff.Value * 100}% (待实现)");
    }

    private void RemoveDefenseBuff(BuffData buff)
    {
        GD.Print("[BuffManager] 防御 Buff 移除 (待实现)");
    }

    private void ApplyHealBonusBuff(BuffData buff)
    {
        GD.Print($"[BuffManager] 治疗加成: +{buff.Value * 100}% (待实现)");
    }

    private void RemoveHealBonusBuff(BuffData buff)
    {
        GD.Print("[BuffManager] 治疗加成 Buff 移除 (待实现)");
    }

    private void ApplyStunBuff(BuffData buff)
    {
        buff.Target?.Movement?.Stop();
        buff.Target?.Attack?.StopAttack();
        GD.Print($"[BuffManager] 眩晕: {buff.Target?.EntityName ?? "null"} (状态系统待实现)");
    }

    private void RemoveStunBuff(BuffData buff)
    {
        GD.Print($"[BuffManager] 眩晕解除: {buff.Target?.EntityName ?? "null"}");
    }

    private void ApplyShieldBuff(BuffData buff)
    {
        GD.Print($"[BuffManager] 护盾: +{buff.Value} (待实现)");
    }

    private void RemoveShieldBuff(BuffData buff)
    {
        GD.Print("[BuffManager] 护盾移除 (待实现)");
    }
}
