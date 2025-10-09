using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace idle_master_game_picker
{
    public partial class Form1 : Form
    {
        // --- Top controls ---
        private readonly TextBox txtApiKey = new() { PlaceholderText = "Steam Web API key", Width = 280 };
        private readonly TextBox txtIdentity = new() { PlaceholderText = "SteamID64 OR profile URL OR vanity name", Width = 420 };
        private readonly Button btnFetch = new() { Text = "Fetch games" };
        private readonly TextBox txtSearch = new() { PlaceholderText = "Search games…", Width = 260 };
        private readonly Button btnSelectAll = new() { Text = "Select all (visible)" };
        private readonly Button btnDeselectAll = new() { Text = "Deselect all (visible)" };
        private readonly Button btnClearAll = new() { Text = "Clear all selections" };
        private readonly CheckedListBox clbGames = new() { Dock = DockStyle.Fill, CheckOnClick = true };

        // --- Bottom controls ---
        private readonly Button btnImportCsv = new() { Text = "Import selections (.csv)" };
        private readonly Button btnExport = new() { Text = "Export (games.ps1 + start.bat + selected_games.csv)" };
        private readonly Label lblStatus = new() { AutoSize = true };

        // --- Advanced parameter controls ---
        private readonly CheckBox chkMaxCoverage = new()
        {
            Text = "Return ALL owned (incl. F2P you played, free subs/giveaways & unvetted)",
            Checked = true,
            AutoSize = true
        };
        private readonly CheckBox chkIncludePlayedFree = new() { Text = "include_played_free_games", Checked = true };
        private readonly CheckBox chkIncludeFreeSub = new() { Text = "include_free_sub", Checked = true };
        private readonly CheckBox chkSkipUnvetted = new() { Text = "skip_unvetted_apps (skip)", Checked = false };

        // --- Help button + tooltip system ---
        private readonly Button btnAdvancedHelp = new()
        {
            Text = "?",
            Width = 28,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(12, 2, 0, 0)
        };

        private readonly ToolTip tip = new()
        {
            AutomaticDelay = 200,
            AutoPopDelay = 20000,
            InitialDelay = 200,
            ReshowDelay = 100
        };

        private List<Game> allGames = new();
        private readonly HashSet<int> selectedAppIds = new(); // persistent selection across filters

        public Form1()
        {
            InitializeComponent();
            Text = "Idle Master Game Picker";
            Width = 980;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;

            // Layout root
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 0 = top
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 1 = advanced
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 2 = list
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 3 = bottom
            Controls.Add(tlp);

            // Top row
            var top = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            top.Controls.Add(new Label { Text = "API key:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
            top.Controls.Add(txtApiKey);
            top.Controls.Add(new Label { Text = "Steam ID / URL / Vanity:", AutoSize = true, Padding = new Padding(12, 6, 6, 0) });
            top.Controls.Add(txtIdentity);
            top.Controls.Add(btnFetch);
            top.Controls.Add(txtSearch);
            top.Controls.Add(btnSelectAll);
            top.Controls.Add(btnDeselectAll);
            top.Controls.Add(btnClearAll);
            tlp.Controls.Add(top, 0, 0);

            // Advanced row (optional parameters)
            var advanced = new GroupBox { Text = "Advanced (optional)", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var advFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Padding = new Padding(6) };

            advFlow.Controls.Add(chkMaxCoverage);
            advFlow.Controls.Add(chkIncludePlayedFree);
            advFlow.Controls.Add(chkIncludeFreeSub);
            advFlow.Controls.Add(chkSkipUnvetted);
            advFlow.Controls.Add(btnAdvancedHelp);
            advanced.Controls.Add(advFlow);
            tlp.Controls.Add(advanced, 0, 1);

            // Tooltips
            tip.SetToolTip(chkMaxCoverage, "When ON: forces include_played_free_games=1, include_free_sub=1, skip_unvetted_apps=false and locks those options.");
            tip.SetToolTip(chkIncludePlayedFree, "include_played_free_games: include Free-to-Play titles you have played.");
            tip.SetToolTip(chkIncludeFreeSub, "include_free_sub: include games from free subs, giveaways, trials, etc.");
            tip.SetToolTip(chkSkipUnvetted, "skip_unvetted_apps: when checked, skip unvetted apps; unchecked = include all.");
            tip.SetToolTip(btnAdvancedHelp, "What do these advanced options mean?");

            btnAdvancedHelp.Click += (_, __) => ShowAdvancedHelp();
            chkMaxCoverage.CheckedChanged += (_, __) => ApplyMaxCoverageMode();

            // Apply initial mode (locks the others if enabled)
            ApplyMaxCoverageMode();

            // Middle (game list)
            tlp.Controls.Add(clbGames, 0, 2);

            // Bottom row
            var bottom = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            bottom.Controls.Add(btnImportCsv);
            bottom.Controls.Add(btnExport);
            bottom.Controls.Add(lblStatus);
            tlp.Controls.Add(bottom, 0, 3);

            // Event hookups
            btnFetch.Click += async (_, __) => await FetchGamesAsync();
            btnExport.Click += (_, __) => ExportAll();
            btnImportCsv.Click += (_, __) => ImportSelectionsCsv();
            txtSearch.TextChanged += (_, __) => ApplyFilter();
            btnSelectAll.Click += (_, __) => SelectAllVisible();
            btnDeselectAll.Click += (_, __) => DeselectAllVisible();
            btnClearAll.Click += (_, __) => ClearAllSelections();
            clbGames.ItemCheck += ClbGames_ItemCheck;

            txtIdentity.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) btnFetch.PerformClick(); };
            txtApiKey.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) btnFetch.PerformClick(); };

            lblStatus.Text = "Ready";

            ApplyDarkThemeToForm();
            ApplyDarkThemeRecursive(this);
            FixLayout();
        }

        // Locks/unlocks advanced toggles and forces values when max coverage is on
        private void ApplyMaxCoverageMode()
        {
            if (chkMaxCoverage.Checked)
            {
                chkIncludePlayedFree.Checked = true;
                chkIncludeFreeSub.Checked = true;
                chkSkipUnvetted.Checked = false;

                chkIncludePlayedFree.Enabled = false;
                chkIncludeFreeSub.Enabled = false;
                chkSkipUnvetted.Enabled = false;
            }
            else
            {
                chkIncludePlayedFree.Enabled = true;
                chkIncludeFreeSub.Enabled = true;
                chkSkipUnvetted.Enabled = true;
            }

            ApplyFilter();
        }

        // ---------------- Steam API ----------------

        private async Task FetchGamesAsync()
        {
            var key = txtApiKey.Text.Trim();
            var identity = txtIdentity.Text.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Please enter your Steam Web API key.", "Missing API key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(identity))
            {
                MessageBox.Show("Please enter your SteamID64, vanity name, or profile URL.", "Missing identity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                UseWaitCursor = true;
                btnFetch.Enabled = false;
                lblStatus.Text = "Resolving SteamID...";

                var steamId = await ResolveSteamIdAsync(key, identity);
                if (steamId == null) return;

                lblStatus.Text = "Fetching games...";

                using var http = new HttpClient();

                var baseUrl = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?";
                var qp = new List<string>
                {
                    $"key={Uri.EscapeDataString(key)}",
                    $"steamid={Uri.EscapeDataString(steamId)}",
                    "include_appinfo=1", // always include names & basic metadata
                    "language=en"        // fixed to English
                };

                bool includePlayedFree = chkMaxCoverage.Checked ? true : chkIncludePlayedFree.Checked;
                bool includeFreeSub = chkMaxCoverage.Checked ? true : chkIncludeFreeSub.Checked;
                bool skipUnvetted = chkMaxCoverage.Checked ? false : chkSkipUnvetted.Checked;

                qp.Add($"include_played_free_games={(includePlayedFree ? "1" : "0")}");
                qp.Add($"include_free_sub={(includeFreeSub ? "1" : "0")}");
                qp.Add($"skip_unvetted_apps={(skipUnvetted ? "true" : "false")}");

                var url = baseUrl + string.Join("&", qp);

                var json = await http.GetStringAsync(url);
                var root = JsonSerializer.Deserialize<OwnedGamesRoot>(json);

                allGames = root?.Response?.Games ?? new();
                selectedAppIds.Clear();
                ApplyFilter();
                lblStatus.Text = $"Loaded {allGames.Count} games.";
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"HTTP error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch games:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error";
            }
            finally
            {
                UseWaitCursor = false;
                btnFetch.Enabled = true;
            }
        }

        private static bool LooksLikeSteamId64(string s)
        {
            if (s.Length < 17) return false;
            foreach (char c in s) if (!char.IsDigit(c)) return false;
            return s.StartsWith("765");
        }

        private async Task<string?> ResolveSteamIdAsync(string apiKey, string input)
        {
            var t = input.Trim().TrimEnd('/');

            if (LooksLikeSteamId64(t)) return t;

            if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(t);
                    var segments = uri.AbsolutePath.Trim('/').Split('/');
                    if (segments.Length >= 2)
                    {
                        if (segments[0].Equals("profiles", StringComparison.OrdinalIgnoreCase) && LooksLikeSteamId64(segments[1]))
                            return segments[1];
                        if (segments[0].Equals("id", StringComparison.OrdinalIgnoreCase))
                            return await ResolveVanityAsync(apiKey, segments[1]);
                    }
                }
                catch { }
            }

            return await ResolveVanityAsync(apiKey, t);
        }

        private static async Task<string?> ResolveVanityAsync(string apiKey, string vanity)
        {
            try
            {
                using var http = new HttpClient();
                var url = $"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key={Uri.EscapeDataString(apiKey)}&vanityurl={Uri.EscapeDataString(vanity)}";
                var json = await http.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<VanityRoot>(json);

                if (data?.Response?.Success == 1 && !string.IsNullOrWhiteSpace(data.Response.SteamId))
                    return data.Response.SteamId;

                MessageBox.Show($"Could not resolve to SteamID64.\nInput: {vanity}", "Resolve failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resolving vanity/profile.\n{ex.Message}", "Resolve error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        // ---------------- Filtering ----------------
        private void ApplyFilter()
        {
            var q = txtSearch.Text.Trim();
            IEnumerable<Game> view = allGames;

            if (!string.IsNullOrEmpty(q))
                view = view.Where(g => (g.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            var ordered = view.OrderBy(g => g.Name ?? "", StringComparer.CurrentCultureIgnoreCase).ToList();

            clbGames.BeginUpdate();
            clbGames.Items.Clear();
            foreach (var g in ordered)
            {
                var item = new GameListItem(g);
                var isChecked = selectedAppIds.Contains(g.AppId);
                clbGames.Items.Add(item, isChecked);
            }
            clbGames.EndUpdate();

            lblStatus.Text = string.IsNullOrEmpty(q)
                ? $"Loaded {allGames.Count} games. Selected {selectedAppIds.Count}."
                : $"Showing {clbGames.Items.Count} / {allGames.Count} (filter active)";
        }

        private void ClbGames_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (e.Index < 0 || e.Index >= clbGames.Items.Count) return;
            if (clbGames.Items[e.Index] is not GameListItem item) return;

            if (e.NewValue == CheckState.Checked)
                selectedAppIds.Add(item.Game.AppId);
            else
                selectedAppIds.Remove(item.Game.AppId);
        }

        private void SelectAllVisible()
        {
            clbGames.BeginUpdate();
            for (int i = 0; i < clbGames.Items.Count; i++)
            {
                if (clbGames.Items[i] is GameListItem item)
                {
                    selectedAppIds.Add(item.Game.AppId);
                    clbGames.SetItemChecked(i, true);
                }
            }
            clbGames.EndUpdate();
            lblStatus.Text = $"Selected {clbGames.CheckedItems.Count} visible. Total selected: {selectedAppIds.Count}.";
        }

        private void DeselectAllVisible()
        {
            clbGames.BeginUpdate();
            for (int i = 0; i < clbGames.Items.Count; i++)
            {
                if (clbGames.Items[i] is GameListItem item)
                {
                    selectedAppIds.Remove(item.Game.AppId);
                    clbGames.SetItemChecked(i, false);
                }
            }
            clbGames.EndUpdate();
            lblStatus.Text = $"Deselected visible. Total selected: {selectedAppIds.Count}.";
        }

        private void ClearAllSelections()
        {
            selectedAppIds.Clear();
            clbGames.BeginUpdate();
            for (int i = 0; i < clbGames.Items.Count; i++)
                clbGames.SetItemChecked(i, false);
            clbGames.EndUpdate();
            lblStatus.Text = "All selections cleared.";
        }

        // ---------------- Export / Import ----------------
        private static string PsSingleQuoted(string? s) => (s ?? "").Replace("'", "''");
        private static string CsvEscape(string? s)
        {
            s ??= "";
            var needsQuotes = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
            if (needsQuotes) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private void ExportAll()
        {
            var selected = allGames.Where(g => selectedAppIds.Contains(g.AppId))
                                   .OrderBy(g => g.Name ?? "", StringComparer.CurrentCultureIgnoreCase)
                                   .ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select at least one game before exporting.", "No selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ps = new StringBuilder();
            ps.AppendLine("# Auto-generated by Steam Games Picker");
            ps.AppendLine("$gameCategories = @(");

            var chunks = Chunk(selected, 30).ToList();
            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var chunk = chunks[ci];
                ps.AppendLine("    @(");

                for (int gi = 0; gi < chunk.Count; gi++)
                {
                    var g = chunk[gi];
                    var safe = PsSingleQuoted(g.Name ?? $"App {g.AppId}");
                    var comma = (gi < chunk.Count - 1) ? "," : "";
                    ps.AppendLine($"        @{{Name='{safe}'; ID={g.AppId}}}{comma}");
                }

                var chunkComma = (ci < chunks.Count - 1) ? "," : "";
                ps.AppendLine($"    ){chunkComma}");
            }
            ps.AppendLine(")");
            ps.AppendLine();
            ps.AppendLine("function Start-Games {");
            ps.AppendLine("    param ($gameList)");
            ps.AppendLine("    foreach ($game in $gameList) {");
            ps.AppendLine("        Write-Host \"$($game.Name) (ID: $($game.ID))\"");
            ps.AppendLine("        Start-Process -FilePath \"steam-idle.exe\" -ArgumentList $game.ID -WindowStyle Minimized");
            ps.AppendLine("    }");
            ps.AppendLine("    Start-Timer -timeout 3600");
            ps.AppendLine("}");
            ps.AppendLine();
            ps.AppendLine("function Stop-Games {");
            ps.AppendLine("    Write-Host \"Stopping all steam-idle.exe processes...\"");
            ps.AppendLine("    Stop-Process -Name \"steam-idle\" -Force -ErrorAction SilentlyContinue");
            ps.AppendLine("    Write-Host \"All steam-idle.exe processes have been stopped.\"");
            ps.AppendLine("    Start-Timer -timeout 30");
            ps.AppendLine("}");
            ps.AppendLine();
            ps.AppendLine("function Start-Timer {");
            ps.AppendLine("    param ([int]$timeout)");
            ps.AppendLine("    $elapsed = 0");
            ps.AppendLine("    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()");
            ps.AppendLine("    while ($elapsed -lt $timeout) {");
            ps.AppendLine("        Write-Host \"`rTime remaining: $($timeout - $elapsed) seconds. Press 'y' to skip: \" -NoNewline");
            ps.AppendLine("        if ([console]::KeyAvailable) {");
            ps.AppendLine("            $key = [console]::ReadKey($true).KeyChar");
            ps.AppendLine("            if ($key -eq \"y\") {");
            ps.AppendLine("                Write-Host \"Skip accepted. Moving forward.\"");
            ps.AppendLine("                return");
            ps.AppendLine("            }");
            ps.AppendLine("        }");
            ps.AppendLine("        Start-Sleep -Seconds 1");
            ps.AppendLine("        $elapsed = [math]::Round($stopwatch.Elapsed.TotalSeconds)");
            ps.AppendLine("    }");
            ps.AppendLine("    Write-Host \"Proceeding to the next step...\"");
            ps.AppendLine("}");
            ps.AppendLine();
            ps.AppendLine("while ($true) {");
            ps.AppendLine("    foreach ($gameList in $gameCategories) {");
            ps.AppendLine("        Clear-Host");
            ps.AppendLine("        Write-Host \"Starting a new game category...\"");
            ps.AppendLine("        Start-Games -gameList $gameList");
            ps.AppendLine("        Stop-Games");
            ps.AppendLine("    }");
            ps.AppendLine("    Write-Host \"Restarting loop...\"");
            ps.AppendLine("}");

            var csv = new StringBuilder();
            csv.AppendLine("appid,name");
            foreach (var g in selected)
                csv.AppendLine($"{g.AppId},{CsvEscape(g.Name ?? $"App {g.AppId}")}");

            using var fbd = new FolderBrowserDialog
            {
                Description = "Select the folder where files will be created."
            };
            if (fbd.ShowDialog(this) != DialogResult.OK)
                return;

            var ps1Path = Path.Combine(fbd.SelectedPath, "games.ps1");
            var batPath = Path.Combine(fbd.SelectedPath, "start.bat");
            var csvPath = Path.Combine(fbd.SelectedPath, "selected_games.csv");

            File.WriteAllText(ps1Path, ps.ToString(), new UTF8Encoding(true));
            var batContent = @"@echo off
title Steam Idle Starter
chcp 65001 >NUL
color 0A
echo Starting PowerShell idle script...
powershell -ExecutionPolicy Bypass -NoProfile -File ""%~dp0games.ps1""
echo.
pause";
            File.WriteAllText(batPath, batContent, new UTF8Encoding(false));

            // Ensure UTF-8 output in PS for non-ASCII names
            var csvText = csv.ToString();
            File.WriteAllText(csvPath, csvText, new UTF8Encoding(true));

            MessageBox.Show(
                $"Created files:\n\n{ps1Path}\n{batPath}\n{csvPath}",
                "Export complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ImportSelectionsCsv()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Import selections CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Multiselect = false
            };
            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            int imported = 0;
            try
            {
                foreach (var line in File.ReadLines(ofd.FileName, new UTF8Encoding(true)))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("appid", StringComparison.OrdinalIgnoreCase))
                        continue;

                    int commaIdx = line.IndexOf(',');
                    var first = (commaIdx >= 0) ? line.Substring(0, commaIdx) : line;
                    if (int.TryParse(first.Trim(), out int appId))
                    {
                        if (selectedAppIds.Add(appId)) imported++;
                    }
                }

                clbGames.BeginUpdate();
                for (int i = 0; i < clbGames.Items.Count; i++)
                {
                    if (clbGames.Items[i] is GameListItem item)
                        clbGames.SetItemChecked(i, selectedAppIds.Contains(item.Game.AppId));
                }
                clbGames.EndUpdate();

                lblStatus.Text = $"Imported {imported} appIDs from CSV. Total selected: {selectedAppIds.Count}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}", "CSV import error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------- Helpers ----------------

        private static IEnumerable<List<T>> Chunk<T>(IEnumerable<T> src, int size)
        {
            var bucket = new List<T>(size);
            foreach (var item in src)
            {
                bucket.Add(item);
                if (bucket.Count == size)
                {
                    yield return bucket;
                    bucket = new List<T>(size);
                }
            }
            if (bucket.Count > 0) yield return bucket;
        }

        private class GameListItem
        {
            public Game Game { get; }
            public GameListItem(Game g) => Game = g;

            public override string ToString()
            {
                var name = string.IsNullOrWhiteSpace(Game.Name) ? $"App {Game.AppId}" : Game.Name;
                return $"{name} (ID: {Game.AppId})";
            }
        }

        private void ShowAdvancedHelp()
        {
            var text =
@"Advanced parameters for IPlayerService/GetOwnedGames/v1:

• Return ALL owned (master switch)
  When ON (default): forces include_played_free_games=1, include_free_sub=1,
  and skip_unvetted_apps=false, and locks the three options below.

• include_played_free_games
  Includes Free-to-Play titles that you have played (claimed/initialized).

• include_free_sub
  Includes games tied to free subscriptions, giveaways, trials, or betas.

• skip_unvetted_apps
  If true, the API skips unvetted/less-public apps.
  Leave unchecked to include as much as possible.";
            MessageBox.Show(this, text, "About Advanced Options", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------------- Dark theme ----------------

        private void ApplyDarkThemeToForm()
        {
            BackColor = Color.FromArgb(32, 32, 35);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10);
        }

        private void ApplyDarkThemeRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                switch (c)
                {
                    case TextBox tb:
                        tb.BackColor = Color.FromArgb(50, 50, 55);
                        tb.ForeColor = Color.WhiteSmoke;
                        tb.BorderStyle = BorderStyle.FixedSingle;
                        break;
                    case CheckedListBox clb:
                        clb.BackColor = Color.FromArgb(45, 47, 48);
                        clb.ForeColor = Color.WhiteSmoke;
                        break;
                    case Button btn:
                        btn.BackColor = Color.FromArgb(70, 72, 75);
                        btn.ForeColor = Color.WhiteSmoke;
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderColor = Color.FromArgb(90, 92, 94);
                        break;
                    case Label lbl:
                        lbl.ForeColor = Color.Gainsboro;
                        break;
                    case GroupBox gb:
                        gb.ForeColor = Color.Gainsboro;
                        break;
                    case CheckBox ck:
                        ck.ForeColor = Color.Gainsboro;
                        break;
                }

                if (c.HasChildren)
                    ApplyDarkThemeRecursive(c);
            }
        }

        private void FixLayout()
        {
            foreach (Control c in this.AllControls())
            {
                switch (c)
                {
                    case Button b:
                        b.FlatStyle = FlatStyle.Flat;
                        b.FlatAppearance.BorderSize = 1;
                        b.BackColor = Color.FromArgb(80, 80, 85);
                        b.ForeColor = Color.WhiteSmoke;
                        b.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                        break;
                    case TextBox t:
                        t.BackColor = Color.FromArgb(50, 50, 55);
                        t.ForeColor = Color.WhiteSmoke;
                        t.BorderStyle = BorderStyle.FixedSingle;
                        break;
                    case Label l:
                        l.ForeColor = Color.WhiteSmoke;
                        break;
                }
            }
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            SuspendLayout();
            // 
            // Form1
            // 
            ClientSize = new Size(284, 261);
            Icon = (Icon)resources.GetObject("appIcon")!;
            Name = "Form1";
            ResumeLayout(false);
        }
    }

    public static class ControlExtensions
    {
        public static IEnumerable<Control> AllControls(this Control control)
        {
            var controls = control.Controls.Cast<Control>();
            return controls.SelectMany(AllControls).Concat(controls);
        }
    }

    // ---------------- JSON models ----------------

    public class OwnedGamesRoot
    {
        [JsonPropertyName("response")] public OwnedGamesResponse? Response { get; set; }
    }

    public class OwnedGamesResponse
    {
        [JsonPropertyName("games")] public List<Game>? Games { get; set; }
    }

    public class Game
    {
        [JsonPropertyName("appid")] public int AppId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    public class VanityRoot
    {
        [JsonPropertyName("response")] public VanityResponse? Response { get; set; }
    }

    public class VanityResponse
    {
        [JsonPropertyName("success")] public int Success { get; set; }
        [JsonPropertyName("steamid")] public string? SteamId { get; set; }
    }
}
