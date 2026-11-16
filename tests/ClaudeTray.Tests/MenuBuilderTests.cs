using ClaudeTray.Core;
using ClaudeTray.Model;
using static ClaudeTray.Tests.AggregatorTests;

namespace ClaudeTray.Tests;

public class MenuBuilderTests
{
    const long Now = 100_000_000;

    [Fact]
    public void SessionRow_IsOneItemWithSubtitle()
    {
        var v = View("1", SessionStatus.Waiting, project: @"C:\projekti\web\web2023", title: "napravi export", detail: "Pita te");
        using var menu = MenuBuilder.Build([v], Now, _ => { }, () => { });

        var item = Assert.IsType<SessionMenuItem>(menu.Items[0]);
        Assert.Equal("web2023 – Pita te", item.Text);
        Assert.Equal("napravi export", item.Subtitle);
        Assert.IsType<ToolStripSeparator>(menu.Items[1]);
        Assert.Equal("Izlaz", menu.Items[2].Text);
        Assert.Equal(3, menu.Items.Count);   // nema zasebne ToolStripLabel stavke
    }

    [Fact]
    public void Subtitle_MarksTerminalSessions()
    {
        var v = View("1", SessionStatus.Working, title: "bok", detail: "Čita");
        v = v with { Record = v.Record with { Entrypoint = "cli" } };
        using var menu = MenuBuilder.Build([v], Now, _ => { }, () => { });
        Assert.Equal("bok · terminal", ((SessionMenuItem)menu.Items[0]).Subtitle);
    }

    [Fact]
    public void ClickOnItem_InvokesCallbackWithSession()
    {
        var v = View("1", SessionStatus.Error, detail: "preopterećeno");
        SessionView? clicked = null;
        using var menu = MenuBuilder.Build([v], Now, s => clicked = s, () => { });
        menu.Items[0].PerformClick();
        Assert.Same(v, clicked);
    }

    [Fact]
    public void TwoLineItem_IsTallerThanOneLine()
    {
        using var menu = new ContextMenuStrip { Font = MenuBuilder.MenuFont };
        var one = new SessionMenuItem("web2023 – Pita te", "", null);
        var two = new SessionMenuItem("web2023 – Pita te", "napravi export u PDF za klijenta", null);
        menu.Items.Add(one);
        menu.Items.Add(two);
        Assert.True(two.GetPreferredSize(Size.Empty).Height > one.GetPreferredSize(Size.Empty).Height);
    }

    [Fact]
    public void MenuFont_Is10pt() => Assert.Equal(10f, MenuBuilder.MenuFont.SizeInPoints);

    // Ručna provjera 16. 11.: nakon prelaska na svijetlu temu izbornik je ostao taman (WinForms boje bira samo pri
    // pokretanju). Izbornik ima vlastitu paletu i prati temu trake pri svakom otvaranju.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Colors_FollowTaskbarTheme(bool light)
    {
        var p = MenuPalette.For(light);
        var v = View("1", SessionStatus.Waiting, title: "naslov", detail: "Pita te");
        using var menu = MenuBuilder.Build([v], Now, _ => { }, () => { }, light);

        Assert.Equal(p.Background, menu.BackColor);
        Assert.All(menu.Items.OfType<ToolStripMenuItem>(), i => Assert.Equal(p.Text, i.ForeColor));
        var table = Assert.IsType<SessionMenuRenderer>(menu.Renderer).ColorTable;
        Assert.Equal(p.Hover, table.MenuItemSelected);
        Assert.Equal(p.Background, table.ToolStripDropDownBackground);
        Assert.Equal(p.Background, table.ImageMarginGradientBegin);
        Assert.Equal(p.Border, table.MenuBorder);
    }

    // Ručna provjera 16. 11.: nakon promjene skaliranja (175 % → 100 %) podnaslov je bio veći od glavnog retka i
    // odrezan, jer je bio zaseban statički font koji WinForms ne skalira s DPI-jem izbornika.
    [Theory]
    [InlineData(5.7f)]
    [InlineData(10f)]
    [InlineData(17.5f)]
    public void Subtitle_ScalesWithItemFont(float size)
    {
        using var font = new Font("Segoe UI", size);
        using var item = new SessionMenuItem("claude-windows-status – Gotovo", "ok, prije nego što nastavim, jedna napomena, na kr", null)
        {
            Font = font,
        };
        Assert.Equal(size * SessionMenuItem.SubtitleScale, item.SubtitleFont.Size, 3);
        Assert.True(item.SubtitleFont.Size < item.Font.Size);
        int needed = TextRenderer.MeasureText(item.Subtitle, item.SubtitleFont).Width;
        Assert.True(item.GetPreferredSize(Size.Empty).Width >= needed, "stavka mora biti dovoljno široka za podnaslov");
    }

    // Ručna provjera 16. 11.: WinForms širinu izbornika računa iz glavnog retka (i stupca prečaca), pa je dulji
    // podnaslov jedine sesije bio odrezan. Nedostajuća širina rezervira se u stupcu prečaca nevidljivim razmacima.
    [Fact]
    public void LongSubtitle_ReservesWidth()
    {
        var v = View("1", SessionStatus.Done, project: @"C:\x\claude-windows-status",
                     title: "ok, prije nego što nastavim, jedna napomena, na kr", detail: "Gotovo");
        using var menu = MenuBuilder.Build([v], Now, _ => { }, () => { });
        var item = (SessionMenuItem)menu.Items[0];

        string reserve = item.ShortcutKeyDisplayString ?? "";
        Assert.NotEmpty(reserve);
        Assert.True(reserve.All(c => c == '\u00A0'));
        int column = TextRenderer.MeasureText(item.Text, item.Font).Width + TextRenderer.MeasureText(reserve, item.Font).Width;
        Assert.True(column >= TextRenderer.MeasureText(item.Subtitle, item.SubtitleFont).Width);
    }

    [Fact]
    public void ShortSubtitle_NoReserve()
    {
        var v = View("1", SessionStatus.Working, project: @"C:\x\mobilna-aplikacija", title: "build", detail: "Pokreće naredbu");
        using var menu = MenuBuilder.Build([v], Now, _ => { }, () => { });
        Assert.True(string.IsNullOrEmpty(((SessionMenuItem)menu.Items[0]).ShortcutKeyDisplayString));
    }

    // Ručna provjera 16. 11.: nakon promjene skaliranja izbornik se nije otvarao iznad ikone. NotifyIcon računa položaj
    // iz veličine izbornika prije stvaranja prozora, a WinForms (PMv2) ga tek pri stvaranju prozora skalira na stvarni
    // DPI. Ručka se zato stvara već pri gradnji, pa je veličina konačna prije računanja položaja.
    [Fact]
    public void Build_CreatesHandleBeforeShow()
    {
        var v = View("1", SessionStatus.Done, detail: "Gotovo");
        using var menu = MenuBuilder.Build([v], Now, _ => { }, () => { });
        Assert.True(menu.IsHandleCreated);
        Assert.False(menu.Visible);
    }

    [Fact]
    public void Palettes_LightIsLightDarkIsDark()
    {
        Assert.True(MenuPalette.For(true).Background.GetBrightness() > 0.9f);
        Assert.True(MenuPalette.For(true).Text.GetBrightness() < 0.2f);
        Assert.True(MenuPalette.For(false).Background.GetBrightness() < 0.25f);
        Assert.True(MenuPalette.For(false).Text.GetBrightness() > 0.9f);
    }
}
