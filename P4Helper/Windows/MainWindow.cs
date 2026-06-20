using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace P4Helper.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly bool?[] isReal    = new bool?[11];
    private              bool hideNames = false;

    //Row sizes
    private const float ColHeader = 26f;
    private const float ColRow = 34f;

    //1st column
    private const float ColLabel       = 120f;
    private const float ColLabelIcons  =  45f;
    private const float ColLabelPadX     = 6f; 
    private const float ColIconSize    =  28f;
    //3rd column
    private const float ColBtn = 43f;
    private const float ColBtns = 97f;

    private const float ColMaxLabelPct = 0.60f;
    private const float ColMaxBtnsPct  = 0.40f;


    private readonly record struct Condition(string Label, string RealText, string FakeText, uint? IconId = null);

    private static readonly (string Header, Condition[] Items)[] Sections =
    [
        ("[Short]",
        [
            new("S-Water", "Stack", "Spread", 215696u),
            new("S-Lightning", "Spread", "Stack", 215623u),
            new("S-Gaze", "Look Away", "Look At", 215588u),
            new("Entropy", "Out", "In", 215902u),
        ]),
        ("[Long]",
        [
            new("L-Water", "Stack", "Spread", 215696u),
            new("L-Lightning", "Spread", "Stack", 215623u),
            new("L-Gaze", "Look Away", "Look At", 215588u),
            new("Fluid", "In", "Out", 215903u),
        ]),
        ("[Misc]",
        [
            new("Line", "Avoid Line", "Stand in Line", 210457u),
            new("Cone", "Avoid Cone", "Stand in Cone", 215501u),
            new("Bomb", "STOP", "MOVE", 215727u),
        ]),
    ];

    private static readonly Vector4 ColorRealRow    = new(0.08f, 0.18f, 0.32f, 1.00f);
    private static readonly Vector4 ColorFakeRow    = new(0.30f, 0.08f, 0.08f, 1.00f);
    private static readonly Vector4 ColorNeutralRow = new(0.12f, 0.12f, 0.15f, 1.00f);
    private static readonly Vector4 ColorSectionBg  = new(0.04f, 0.04f, 0.04f, 1.00f);
    private static readonly Vector4 ColorSectionFg  = new(1.00f, 1.00f, 1.00f, 0.85f);
    private static readonly Vector4 ColorResult     = new(1.00f, 0.85f, 0.00f, 1.00f);
    private static readonly Vector4 ColorResultNone = new(0.50f, 0.50f, 0.50f, 1.00f);

    private static readonly Vector4 ColorRealActive = new(0.20f, 0.60f, 1.00f, 1.00f);
    private static readonly Vector4 ColorRealHov    = new(0.30f, 0.70f, 1.00f, 0.90f);
    private static readonly Vector4 ColorFakeActive = new(0.82f, 0.14f, 0.28f, 1.00f);
    private static readonly Vector4 ColorFakeHov    = new(0.95f, 0.22f, 0.38f, 0.90f);
    private static readonly Vector4 ColorInactive   = new(0.16f, 0.16f, 0.20f, 1.00f);
    private static readonly Vector4 ColorInactiveHov= new(0.26f, 0.26f, 0.30f, 1.00f);

    private static readonly Vector4 ColorReset      = new(0.85f, 0.15f, 0.15f, 1.00f);
    private static readonly Vector4 ColorResetHov   = new(1.00f, 0.30f, 0.30f, 1.00f);

    public MainWindow(Plugin plugin)
        : base("P4 Helper##P4HelperWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 520),
            MaximumSize = new Vector2(500, 520),
        };
    }

    public void Dispose() { }
    
    public override void Draw()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Checkbox("Hide icon names", ref hideNames);

        var resetSize = new Vector2(70 * ImGuiHelpers.GlobalScale, 0);
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - resetSize.X + ImGui.GetCursorPosX());
        using (ImRaii.PushColor(ImGuiCol.Button,        ColorReset))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, ColorResetHov))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive,  ColorReset))
        {
            if (ImGui.Button("RESET", resetSize))
                Array.Clear(isReal);
        }

        ImGui.Separator();

        var contentWidth = ImGui.GetContentRegionAvail().X;

        using var table = ImRaii.Table("##p4tbl", 3,
            ImGuiTableFlags.BordersInnerH   |
            ImGuiTableFlags.BordersInnerV   |
            ImGuiTableFlags.SizingFixedFit  |
            ImGuiTableFlags.NoSavedSettings);
        if (!table.Success) return;

        var minPx = 20f  * ImGuiHelpers.GlobalScale;
        var maxLbl  = contentWidth * ColMaxLabelPct;
        var maxBtns = contentWidth * ColMaxBtnsPct;

        var lblWidth = hideNames ? ColLabelIcons : ColLabel;
        var clampedLblWidth = Math.Clamp(lblWidth * ImGuiHelpers.GlobalScale, minPx, maxLbl);
        var clampedBtnsWidth = Math.Clamp(ColBtns * ImGuiHelpers.GlobalScale, minPx, maxBtns);

        ImGui.TableSetupColumn("##lbl",    ImGuiTableColumnFlags.WidthFixed,   clampedLblWidth);
        ImGui.TableSetupColumn("##result", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##btns",   ImGuiTableColumnFlags.WidthFixed,   clampedBtnsWidth);

        var btnSize  = new Vector2(ColBtn * ImGuiHelpers.GlobalScale, 26 * ImGuiHelpers.GlobalScale);
        var stateIdx = 0;

        foreach (var (header, items) in Sections)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, ColHeader * ImGuiHelpers.GlobalScale);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                ImGui.ColorConvertFloat4ToU32(ColorSectionBg));
            ImGui.TableNextColumn();

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ColLabelPadX * ImGuiHelpers.GlobalScale);

            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(ColorSectionFg, header);
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            foreach (var cond in items)
            {
                var state = isReal[stateIdx];

                var bgCol = ImGui.ColorConvertFloat4ToU32(state switch
                {
                    true  => ColorRealRow,
                    false => ColorFakeRow,
                    null  => ColorNeutralRow,
                });

                ImGui.TableNextRow(ImGuiTableRowFlags.None, ColRow * ImGuiHelpers.GlobalScale);
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, bgCol);

                ImGui.TableNextColumn();

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ColLabelPadX * ImGuiHelpers.GlobalScale);

                if (cond.IconId.HasValue)
                {
                    var iconTex = Plugin.TextureProvider
                        .GetFromGameIcon(new GameIconLookup(cond.IconId.Value))
                        .GetWrapOrDefault();
                    if (iconTex != null)
                    {
                        var rowH   = ColIconSize * ImGuiHelpers.GlobalScale;
                        var aspect = iconTex.Size.X / iconTex.Size.Y;
                        ImGui.Image(iconTex.Handle, new Vector2(rowH * aspect, rowH));
                        if (!hideNames) ImGui.SameLine();
                    }
                }
                if (!hideNames)
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(cond.Label);
                }

                ImGui.TableNextColumn();
                var (resultText, resultColor) = state switch
                {
                    true  => (cond.RealText, ColorResult),
                    false => (cond.FakeText, ColorResult),
                    null  => ("—",           ColorResultNone),
                };
                var avail = ImGui.GetContentRegionAvail().X;
                var tw    = ImGui.CalcTextSize(resultText).X;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (avail - tw) / 2f));
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(resultColor, resultText);

                var realOn = state == true;
                var fakeOn = state == false;
                ImGui.TableNextColumn();

                using (ImRaii.PushColor(ImGuiCol.Button,        realOn ? ColorRealActive : ColorInactive))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, realOn ? ColorRealHov    : ColorInactiveHov))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive,  realOn ? ColorRealActive : ColorInactive))
                using (ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon))
                {
                    if (ImGui.Button($"{FontAwesomeIcon.Circle.ToIconString()}##real{stateIdx}", btnSize))
                        isReal[stateIdx] = true;
                }

                ImGui.SameLine();

                using (ImRaii.PushColor(ImGuiCol.Button,        fakeOn ? ColorFakeActive : ColorInactive))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, fakeOn ? ColorFakeHov    : ColorInactiveHov))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive,  fakeOn ? ColorFakeActive : ColorInactive))
                using (ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon))
                {
                    if (ImGui.Button($"{FontAwesomeIcon.Question.ToIconString()}##fake{stateIdx}", btnSize))
                        isReal[stateIdx] = false;
                }

                stateIdx++;
            }
        }
    }
}
