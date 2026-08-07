using OutpostProtocol.Gameplay.Entity;
using System;

namespace OutpostProtocol.Data;

/// <summary>Buff 类型枚举</summary>
public enum BuffType
{
    Attack, // 攻击力加成
    AttackSpeed, // 攻击速度加成
    Defense, // 防御力加成
    HealBonus, // 治疗量加成
    MoveSpeed, // 移动速度加成
    Stun, // 眩晕
    Shield, // 护盾
}

/// <summary>Buff 运行时数据结构</summary>
public class BuffData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public BaseEntity Target { get; set; }
    public BuffType Type { get; set; }
    public float Value { get; set; } // 加成值（百分比，如 0.5 = 50%）
    public float RemainingTime { get; set; } // 剩余时间（秒）
    public float Duration { get; set; } // 总持续时间（秒）
    public object OriginalValue { get; set; } // 原始值（用于还原）
    public Action<BaseEntity> OnApply { get; set; }
    public Action<BaseEntity> OnRemove { get; set; }
    public Action<BaseEntity, float> OnTick { get; set; }
    public bool IsExpired => RemainingTime <= 0;
    public string SourceSkillId { get; set; }
}
