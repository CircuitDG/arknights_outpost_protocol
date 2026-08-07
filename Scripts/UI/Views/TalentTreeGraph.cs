using Godot;
using System.Collections.Generic;
using System.Linq;

namespace OutpostProtocol.UI.Views;

/// <summary>
/// 天赋树图（树状布局）
/// 按 tier 分列摆放天赋卡片，并绘制前置连线
/// </summary>
public partial class TalentTreeGraph : Control
{
    private const float CardWidth = 230;
    private const float CardHeight = 84;
    private const float GapX = 46;
    private const float GapY = 18;

    private readonly List<(Vector2 From, Vector2 To)> _edges = new();
    private readonly List<TalentCard> _cards = new();

    public void Setup(IEnumerable<TalentCard> cards, IEnumerable<(string ParentId, string ChildId)> edges)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }
        _cards.Clear();
        _edges.Clear();

        var cardList = cards.ToList();
        var byId = cardList
            .Where(c => c.Talent != null)
            .ToDictionary(c => c.Talent.Id);

        int maxTier = cardList.Count > 0 ? cardList.Max(c => c.Talent?.Tier ?? 0) : 0;
        int maxInTier = 0;
        var perTier = new Dictionary<int, int>();
        foreach (var card in cardList)
        {
            int tier = card.Talent?.Tier ?? 0;
            perTier[tier] = perTier.GetValueOrDefault(tier) + 1;
            maxInTier = Mathf.Max(maxInTier, perTier[tier]);
        }

        var rowsInTier = new Dictionary<int, int>();
        foreach (var card in cardList)
        {
            int tier = card.Talent?.Tier ?? 0;
            int row = rowsInTier.GetValueOrDefault(tier);
            rowsInTier[tier] = row + 1;

            var pos = new Vector2(
                tier * (CardWidth + GapX),
                row * (CardHeight + GapY)
            );
            card.Position = pos;
            card.Size = new Vector2(CardWidth, CardHeight);
            card.MouseFilter = MouseFilterEnum.Stop;
            AddChild(card);
            _cards.Add(card);
        }

        foreach (var (parentId, childId) in edges)
        {
            if (!byId.TryGetValue(parentId, out var parent) ||
                !byId.TryGetValue(childId, out var child))
            {
                continue;
            }

            Vector2 from = parent.Position + new Vector2(CardWidth, CardHeight * 0.5f);
            Vector2 to = child.Position + new Vector2(0, CardHeight * 0.5f);
            _edges.Add((from, to));
        }

        float width = (maxTier + 1) * (CardWidth + GapX) - GapX + 30;
        float height = maxInTier * (CardHeight + GapY) - GapY + 30;
        CustomMinimumSize = new Vector2(Mathf.Max(1, width), Mathf.Max(1, height));

        QueueRedraw();
    }

    public override void _Draw()
    {
        var lineColor = new Color(0.72f, 0.58f, 0.32f, 0.9f);
        var dotColor = new Color(0.95f, 0.78f, 0.35f, 1f);

        foreach (var edge in _edges)
        {
            Vector2 mid = new Vector2((edge.From.X + edge.To.X) * 0.5f, edge.From.Y);
            DrawLine(edge.From, mid, lineColor, 2.5f);
            DrawLine(mid, new Vector2(mid.X, edge.To.Y), lineColor, 2.5f);
            DrawLine(new Vector2(mid.X, edge.To.Y), edge.To, lineColor, 2.5f);
            DrawCircle(edge.From, 4f, dotColor);
            DrawCircle(edge.To, 4f, dotColor);
        }
    }
}
