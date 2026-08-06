using Godot;
using OutpostProtocol.Core.EventBus;
using System;
using System.Collections.Generic;

namespace OutpostProtocol.Gameplay.Entity.Components;

/// <summary>
/// 技能组件
/// 职责：技能定义、冷却管理、释放执行
/// </summary>
public partial class SkillComponent : Node
{
    /// <summary>技能数据类</summary>
    public class SkillData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float Cooldown { get; set; } = 5.0f;
        public float CastTime { get; set; }
        public int Cost { get; set; } // 消耗（如体力）
        public Action<BaseEntity> OnCast { get; set; } // 技能效果
    }

    // ============================================================
    // 运行时状态
    // ============================================================

    private BaseEntity _owner;
    private Dictionary<string, SkillData> _skills = new();
    private Dictionary<string, float> _cooldowns = new();
    private string _activeSkillId = string.Empty;

    // ============================================================
    // 事件
    // ============================================================

    public event Action<string> SkillCasted;
    public event Action<string, float> CooldownUpdated;

    // ============================================================
    // 公共属性
    // ============================================================

    public string ActiveSkillId => _activeSkillId;

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
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 更新所有技能的冷却
        var keys = new List<string>(_cooldowns.Keys);
        foreach (var key in keys)
        {
            if (_cooldowns[key] > 0)
            {
                _cooldowns[key] = Math.Max(0, _cooldowns[key] - dt);
                CooldownUpdated?.Invoke(key, _cooldowns[key]);
            }
        }
    }

    // ============================================================
    // 技能管理
    // ============================================================

    /// <summary>注册技能</summary>
    public void RegisterSkill(SkillData skill)
    {
        if (_skills.ContainsKey(skill.Id))
        {
            GD.PushWarning($"[SkillComponent] 技能 {skill.Id} 已存在");
            return;
        }

        _skills[skill.Id] = skill;
        _cooldowns[skill.Id] = 0;
        GD.Print($"[SkillComponent] 注册技能: {skill.Id}");
    }

    /// <summary>释放技能</summary>
    public bool CastSkill(string skillId)
    {
        if (!_skills.TryGetValue(skillId, out var skill))
        {
            GD.PushWarning($"[SkillComponent] 技能 {skillId} 不存在");
            return false;
        }

        // 检查冷却
        if (_cooldowns[skillId] > 0)
        {
            GD.Print($"[SkillComponent] 技能 {skillId} 冷却中 ({_cooldowns[skillId]}s)");
            return false;
        }

        // 执行技能
        skill.OnCast?.Invoke(_owner);

        // 进入冷却
        _cooldowns[skillId] = skill.Cooldown;
        _activeSkillId = skillId;

        SkillCasted?.Invoke(skillId);
        EventBus.Instance.EmitLogMessage($"{_owner.EntityName} 释放技能: {skill.Name}", "INFO");

        GD.Print($"[SkillComponent] 释放技能: {skill.Name}");
        return true;
    }

    /// <summary>获取技能冷却进度 (0-1)</summary>
    public float GetCooldownProgress(string skillId)
    {
        if (!_skills.TryGetValue(skillId, out var skill)) return 1.0f;
        if (!_cooldowns.TryGetValue(skillId, out var cd)) return 1.0f;

        return skill.Cooldown > 0 ? cd / skill.Cooldown : 0f;
    }

    /// <summary>获取技能是否可用</summary>
    public bool IsSkillReady(string skillId)
    {
        if (!_cooldowns.TryGetValue(skillId, out var cd)) return false;
        return cd <= 0;
    }
}
