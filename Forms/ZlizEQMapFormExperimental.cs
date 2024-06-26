﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Drawing2D;
using TheArtOfDev.HtmlRenderer.WinForms;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Configuration;
using ZlizEQMap.Forms;

namespace ZlizEQMap
{
    public partial class ZlizEQMapFormExperimental : Form
    {
        CartographyService Cartographer;
        DateTime LastRecordedZoneChange = DateTime.Now;

        bool initialLoadCompleted = false;
        int flowSubMapssubMapsTotalHeight = 1;
        float renderScale = 1.0F;
        public static Dictionary<PictureBox, ZoneData> subMapPictureBoxes = new Dictionary<PictureBox, ZoneData>();
        PopoutMap popoutMap;

        public ZlizEQMapFormExperimental()
        {
            InitializeComponent();
            SetControlProperties();
            Initialize();
        }

        private void ZlizEQMapFormExperimental_Load(object sender, EventArgs e)
        {

        }

        private void Initialize()
        {
            Cartographer = new CartographyService();
            CartographyService.RedrawMapsEH += ZlizEQMapFormExperimental_CartographerSaysRedraw;
            PopulateZoneComboBox();
            SetControlsFromSettings();
            Cartographer.OSSwitchToZoneByShortName(Settings.LastSelectedZone);

            initialLoadCompleted = true;

            if (checkAutoSizeOnMapSwitch.Checked)
                AutoSizeForm();

            BindNoteDataToDGV();

            timer_ParseLogsTimer.Enabled = true;
            timer_ParseLogsTimer.Interval = Settings.AutoParseIntervalMS;

            btnAutosize.Select();
            picBox.Invalidate();
        }

        private void ZlizEQMapFormExperimental_CartographerSaysRedraw(object sender, EventArgs e)
        {
            GetLatestCartographyData();
        }

        private void GetLatestCartographyData()
        {
            if (LastRecordedZoneChange < Cartographer.LastRecordedZoneChange)
            {
                ZoneChangedUpdateUI();
            }
            if (check_AutoUpdateNoteLocation.Checked)
            {
                SetNoteCoordsToPlayerLocation();
            }
            picBox.Invalidate();
        }

        private void SetControlsFromSettings()
        {
            checkAutoSizeOnMapSwitch.Checked = Settings.CheckAutoSizeOnMapSwitch;
            checkGroupByContinent.Checked = Settings.CheckGroupByContinent;
            checkEnableDirection.Checked = Settings.CheckEnableDirection;
            checkLegend.Checked = Settings.CheckEnableLegend;

            sliderOpacity.Value = Settings.OpacityLevel;
            SetFormOpacity();

            checkAlwaysOnTop.Checked = Settings.AlwaysOnTop;
            SetAlwaysOnTop();

            check_AutosaveNotes.Checked = Settings.NotesAutoSave;
            check_AutoUpdateNoteLocation.Checked = Settings.NotesAutoUpdate;
            check_ClearNoteAfterEntry.Checked = Settings.NotesClearAfterEntry;
            check_ShowAnnotations.CheckState = (CheckState)Settings.NotesShow;
            check_ShowPlayerLocHistory.Checked = Settings.LocHistoryShow;

            nud_HistoryToTrack.Value = Settings.LocHistoryNumberToTrack;
            button_NotesFont.Font = Settings.NotesFont;
            button_NotesColor.BackColor = Settings.NotesColor;

            nud_ParseTimerMS.Value = Settings.AutoParseIntervalMS;
        }

        private void PopulateZoneComboBox()
        {
            comboZone.Items.Clear();

            if (!checkGroupByContinent.Checked)
            {
                foreach (ZoneData map in Cartographer.Zones.Where(x => x.SubMapIndex == 1).OrderBy(x => x.FullName))
                {
                    comboZone.Items.Add(map.FullName);
                }
            }
            else
            {
                List<string> continents = new List<string>();

                foreach (ZoneData map in Cartographer.Zones.OrderBy(x => x.ContinentSortOrder))
                {
                    if (!continents.Contains(map.Continent))
                        continents.Add(map.Continent);
                }

                foreach (string continent in continents)
                {
                    comboZone.Items.Add(String.Format("-------------------- {0} --------------------", continent));

                    foreach (ZoneData map in Cartographer.Zones.Where(x => x.Continent == continent && x.SubMapIndex == 1).OrderBy(x => x.FullName))
                    {
                        comboZone.Items.Add(map.FullName);
                    }
                }
            }

            if (Cartographer.CurrentZoneData != null)
                comboZone.SelectedItem = Cartographer.CurrentZoneData.FullName;
        }

        private void SetFormTitle(string newTitle)
        {
            this.Text = newTitle;
        }

        private void ZoneChangedUpdateUI()
        {
            LastRecordedZoneChange = DateTime.Now;
            labelZoneName.Text = Cartographer.CurrentZoneData.FullName;
            picBox.Load(Cartographer.CurrentZoneData.ImageFilePath);
            SetFormTitle(Cartographer.CurrentZoneData.FullName);
            PopulateSubMaps(Cartographer.CurrentZoneData);
            comboZone.SelectedItem = Cartographer.CurrentZoneData.FullName;
            labelLegend.Text = LegendTextFactory.GetLegendText(Cartographer.CurrentZoneData);

            PopulateConnectedZones();
            SetButtonWaypointText();
            RefreshZoneAnnotationList();
            ResizeMap();

            if (popoutMap != null)
            {
                popoutMap.LoadMapByPath(Cartographer.CurrentZoneData.ImageFilePath);
            }

            if (checkAutoSizeOnMapSwitch.Checked)
                AutoSizeForm();
        }

        private void SwitchZone(string zoneName, int subMapIndex)
        {
            if (!Cartographer.OSSwitchToZone(zoneName, subMapIndex))
            {
                MessageBox.Show(String.Format("Unable to find zone '{0}' in Zone Data", zoneName), "ZlizEQMap", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void PopulateConnectedZones()
        {
            if (Cartographer.CurrentZoneData.ConnectedZones.Count > 0)
            {
                panelConnectedZones.Visible = true;
                panelConnectedZones.Controls.Clear();
                panelConnectedZones.Controls.Add(labelConnectedZones);
                panelConnectedZones.Text = "Connected Zones:";

                foreach (ZoneData zone in Cartographer.CurrentZoneData.ConnectedZones.OrderBy(x => x.FullName))
                {
                    LinkLabel ll = new LinkLabel();
                    ll.Text = zone.FullName;
                    ll.AutoSize = true;
                    ll.Name = zone.FullName;
                    ll.LinkClicked += new LinkLabelLinkClickedEventHandler(ll_LinkClicked);

                    this.panelConnectedZones.Controls.Add(ll);
                }
            }
            else
                panelConnectedZones.Visible = false;
        }

        void ll_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            SwitchZone((sender as LinkLabel).Text, 1);
        }

        private void PopulateSubMaps(ZoneData zoneData)
        {
            flowSubMaps.Controls.Clear();
            subMapPictureBoxes.Clear();
            flowSubMaps.Height = 1;
            flowSubMaps.Visible = false;
            var subZones = Cartographer.Zones.Where(x => x.FullName == zoneData.FullName);
            int i = 0;

            if (subZones.Count() > 1)
            {
                foreach (ZoneData zone in subZones.OrderBy(x => x.SubMapIndex))
                {
                    Color borderColor = Color.Transparent;

                    if (zone.SubMapIndex == zoneData.SubMapIndex)
                        borderColor = Color.MediumPurple;

                    ZlizPanel panel = new ZlizPanel(borderColor);
                    panel.Size = new System.Drawing.Size(102, 102);
                    panel.Location = new Point(0, i++ * 101);

                    ZlizPictureBox pb = new ZlizPictureBox();
                    pb.SizeMode = PictureBoxSizeMode.StretchImage;
                    pb.WaitOnLoad = true;
                    pb.Load(zone.ImageFilePath);
                    pb.Size = new System.Drawing.Size(100, 100);
                    pb.Location = new Point(1, 1);
                    pb.Click += new EventHandler(pb_Click);
                    pb.MouseHover += new EventHandler(pb_MouseHover);
                    pb.SubMapDescription = zone.SubMapDescription;

                    panel.Controls.Add(pb);

                    flowSubMaps.Controls.Add(panel);
                    subMapPictureBoxes.Add(pb, zone);
                }

                flowSubMapssubMapsTotalHeight = i * 110;
                ReCalcFlowSubMapsHeight();
                flowSubMaps.Visible = true;
            }
        }

        void pb_Click(object sender, EventArgs e)
        {
            PictureBox pb = (sender as PictureBox);

            ZoneData zoneData = subMapPictureBoxes[pb];

            SwitchZone(zoneData.FullName, zoneData.SubMapIndex);
        }

        void pb_MouseHover(object sender, EventArgs e)
        {
            ZlizPictureBox pb = sender as ZlizPictureBox;

            if (pb.SubMapDescription != null)
            {
                ToolTip tt = new ToolTip() { InitialDelay = 200 };

                tt.SetToolTip(pb, pb.SubMapDescription);
            }
        }

        private void picBox_Paint(object sender, PaintEventArgs e)
        {
            Cartographer.PrepMapMarkersForCanvas(sender, e, checkEnableDirection.Checked);

            foreach (IMapDrawable marker in Cartographer.Markers)
            {
                marker.Draw(e.Graphics, renderScale);
            }
        }

        private void checkEnableDirection_CheckedChanged(object sender, EventArgs e)
        {
            Cartographer.RaiseRedrawMaps(null, null);
        }

        private void checkLegend_CheckedChanged(object sender, EventArgs e)
        {
            labelLegend.Visible = checkLegend.Checked;

            if (checkAutoSizeOnMapSwitch.Checked)
                AutoSizeForm();
        }

        private void checkGroupByContinent_CheckedChanged(object sender, EventArgs e)
        {
            if (initialLoadCompleted)
                PopulateZoneComboBox();
        }

        private void comboZone_DropDownClosed(object sender, EventArgs e)
        {
            if (!comboZone.SelectedItem.ToString().StartsWith("-") && initialLoadCompleted)
                SwitchZone(comboZone.SelectedItem.ToString(), 1);
        }

        private void btnAutosize_Click(object sender, EventArgs e)
        {
            AutoSizeForm();
        }

        private void AutoSizeForm()
        {
            if (initialLoadCompleted)
            {
                picBox.SizeMode = PictureBoxSizeMode.AutoSize;

                int width = 1025;
                if (picBox.Width + 60 > width)
                    width = picBox.Width + 70;

                var subMaps = Cartographer.Zones.Where(x => x.FullName == Cartographer.CurrentZoneData.FullName);

                if (subMaps.Count() > 1)
                    width += flowSubMaps.Width;

                this.Width = width;

                int legendHeight = labelLegend.Height;

                if (!checkLegend.Checked)
                    legendHeight = 10;

                int height = labelZoneName.Height + picBox.Height + legendHeight + 105;

                ////Rectangle working = Screen.GetWorkingArea(this.splitContainerMain);
                //if (height > splitContainerMain.Height + 100)
                //    height = working.Height + 100;

                this.Height = height;
            }
        }

        private void ZlizEQMapFormExperimental_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cartographer.UnLoad();

            Settings.CheckAutoSizeOnMapSwitch = checkAutoSizeOnMapSwitch.Checked;
            Settings.CheckEnableDirection = checkEnableDirection.Checked;
            Settings.CheckEnableLegend = checkLegend.Checked;
            Settings.CheckGroupByContinent = checkGroupByContinent.Checked;

            if (Cartographer.CurrentZoneData != null)
                Settings.LastSelectedZone = Cartographer.CurrentZoneData.ShortName;

            Settings.OpacityLevel = sliderOpacity.Value;
            Settings.AlwaysOnTop = checkAlwaysOnTop.Checked;
            Settings.NotesClearAfterEntry = check_ClearNoteAfterEntry.Checked;
            Settings.NotesAutoUpdate = check_AutoUpdateNoteLocation.Checked;
            Settings.NotesAutoSave = check_AutosaveNotes.Checked;
            Settings.NotesShow = (int)check_ShowAnnotations.CheckState;

            Settings.LocHistoryShow = check_ShowPlayerLocHistory.Checked;
            Settings.LocHistoryNumberToTrack = (int)nud_HistoryToTrack.Value;
            Settings.AutoParseIntervalMS = (int)nud_ParseTimerMS.Value;

            Settings.SaveSettings();
        }

        private void ReCalcFlowSubMapsHeight()
        {
            //so we don't hide icons offscren if map is taller than window
            flowSubMaps.Height = Math.Min(flowSubMapssubMapsTotalHeight, panelMain.Height); //check size of number of SubMaps vs main map height 
            flowSubMaps.Height = Math.Min(flowSubMaps.Height, this.Height); //so we don't hide icons offscren if map is taller than window
        }

        private void ZlizEQMapFormExperimental_SizeChanged(object sender, EventArgs e)
        {

            ReCalcFlowSubMapsHeight();

            //labelLegend.MaximumSize = new Size(this.Width - 50, 1000000);
        }

        private void linkLabelWiki_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(String.Format("{0}{1}", Settings.WikiRootURL, Cartographer.CurrentZoneData.FullName));
        }

        private void btnSettingsHelp_Click(object sender, EventArgs e)
        {
            SettingsHelp settingsHelp = new SettingsHelp();
            settingsHelp.OnEQDirSettingsChanged += new EventHandler(settingsHelp_OnEQDirSettingsChanged);

            settingsHelp.ShowDialog();
        }

        void settingsHelp_OnEQDirSettingsChanged(object sender, EventArgs e)
        {
            Cartographer.EQDirSettingsChanged();
            Initialize();
        }

        private void sliderOpacity_Scroll(object sender, EventArgs e)
        {
            SetFormOpacity();
        }

        private void SetFormOpacity()
        {
            this.Opacity = sliderOpacity.Value / 100f;
        }

        private void ZlizEQMapFormExperimental_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && Settings.MinimizeToTray)
            {
                notifyIcon1.Visible = true;
                this.Hide();
            }
        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            this.Show();
            notifyIcon1.Visible = false;
            this.WindowState = FormWindowState.Normal;
        }

        private void checkAlwaysOnTop_CheckedChanged(object sender, EventArgs e)
        {
            SetAlwaysOnTop();
        }

        private void SetAlwaysOnTop()
        {
            this.TopMost = checkAlwaysOnTop.Checked;
        }

        private Point ParseTextToPoint(string text)
        {
            Point result = new Point(0, 0);
            string[] items = text.Split(',');

            if (items.Length == 2)
            {
                int y;
                int x;

                if (Int32.TryParse(items[0].Trim(), out y) && Int32.TryParse(items[1].Trim(), out x))
                {
                    result.X = x;
                    result.Y = y;
                }
            }

            return result;
        }

        private void ForceSetWaypoint()
        {
            Point parsedWaypoint = ParseTextToPoint(txtWaypoint.Text);
            Cartographer.SetWaypoint(parsedWaypoint.X, parsedWaypoint.Y);
            SetButtonWaypointText();
        }

        private void SetButtonWaypointText()
        {
            if (Cartographer.Waypoint == null)
                btnSetWaypoint.Text = "Set WP";
            else
                btnSetWaypoint.Text = "Clear";
        }

        private void textBoxCharName_TextChanged(object sender, EventArgs e)
        {
            Cartographer.CharacterNameChange(textBoxCharName.Text);
        }

        private void InitializeDebugging()
        {
            picBox.SizeMode = PictureBoxSizeMode.Zoom;
            ForceSetPlayerLocation();
            ForceSetWaypoint();
            ResizeMap();
        }

        private void btnSetWaypoint_Click(object sender, EventArgs e)
        {
            if (Cartographer.Waypoint == null)
            {
                string[] items = txtWaypoint.Text.Split(',');

                if (items.Length == 2)
                {
                    int y;
                    int x;

                    if (Int32.TryParse(items[0].Trim(), out y) && Int32.TryParse(items[1].Trim(), out x))
                    {
                        Cartographer.SetWaypoint(x, y);
                    }
                }
            }
            else
                Cartographer.ClearWaypoint();

            SetButtonWaypointText();
        }

        private void ForceSetPlayerLocation()
        {
            Cartographer.UpdatePlayerLocation((int)nud_playerX.Value, (int)nud_playerY.Value);
        }

        private void ResizeMap()
        {
            var roundedX = Math.Round(Cartographer.CurrentZoneMap.ImageX * renderScale);
            var roundedY = Math.Round(Cartographer.CurrentZoneMap.ImageY * renderScale);

            picBox.Width = (int)roundedX;
            picBox.Height = (int)roundedY;

            //ForceSetWaypoint();
            //ForceSetPlayerLocation();
        }

        private void buttonAutosize_Click(object sender, EventArgs e)
        {
            picBox.SizeMode = PictureBoxSizeMode.AutoSize;
        }

        private void buttonStretch_Click(object sender, EventArgs e)
        {
            picBox.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        private void buttonCenter_Click(object sender, EventArgs e)
        {
            picBox.SizeMode = PictureBoxSizeMode.CenterImage;
        }

        private void buttonZoom_Click(object sender, EventArgs e)
        {
            picBox.SizeMode = PictureBoxSizeMode.Zoom;
        }

        private void buttonNormal_Click(object sender, EventArgs e)
        {
            picBox.SizeMode = PictureBoxSizeMode.Normal;
        }

        private void buttonForceRedraw_Click(object sender, EventArgs e)
        {
            ResizeMap();
        }

        private void nud_scale_ValueChanged(object sender, EventArgs e)
        {
            float ratio = (float)nud_scale.Value;
            renderScale = ratio;
            ResizeMap();
        }

        private void buttonSetPlayerXY_Click(object sender, EventArgs e)
        {
            ForceSetPlayerLocation();
        }

        private void nud_playerY_ValueChanged(object sender, EventArgs e)
        {
            ForceSetPlayerLocation();
        }

        private void nud_playerX_ValueChanged(object sender, EventArgs e)
        {
            ForceSetPlayerLocation();
        }

        private void button_reinit_Click(object sender, EventArgs e)
        {
            InitializeDebugging();
        }

        private void sliderZoom_Scroll(object sender, EventArgs e)
        {
            SetMapZoomFromSlider();
        }

        private void button_ResetZoom_Click(object sender, EventArgs e)
        {
            ResetZoom();
        }

        private void ResetZoom()
        {
            ResetZoomValue();
            SetMapZoomFromSlider();
        }

        private void ResetZoomValue()
        {
            sliderZoom.Value = 100;
        }

        private void SetMapZoomFromSlider()
        {
            picBox.SizeMode = PictureBoxSizeMode.Zoom;
            float sliderValue = (float)sliderZoom.Value;
            float calc = sliderValue / 100F;
            renderScale = calc;
            ResizeMap();
        }

        public void OpenPopoutMap()
        {
            popoutMap = new PopoutMap(Cartographer);
            popoutMap.LoadMapByPath(Cartographer.CurrentZoneData.ImageFilePath);
            popoutMap.Show();
        }

        private void buttonOpenPopoutMap_Click(object sender, EventArgs e)
        {
            OpenPopoutMap();
        }
        private void BindNoteDataToDGV()
        {
            if (initialLoadCompleted)
            {
                zoneAnnotationBindingSource.DataSource = Cartographer.CurrentZoneAnnotations;
                dgv_ZoneAnnotation.DataSource = zoneAnnotationBindingSource;
                dgv_ZoneAnnotation.AutoGenerateColumns = false;
                dgv_ZoneAnnotation.AutoSize = false;
            }
        }

        private void button_AddNote_Click(object sender, EventArgs e)
        {
            Point noteCoords = ParseTextToPoint(txt_NewNoteCoords.Text);

            Cartographer.AddNote(txt_NewNote.Text, noteCoords.X, noteCoords.Y);

            if (check_AutosaveNotes.Checked)
            {
                Cartographer.SaveNotes();
            }

            if (check_ClearNoteAfterEntry.Checked)
            {
                txt_NewNote.Clear();
                txt_NewNoteCoords.Clear();
                txt_NewNoteCoords.Text = "0, 0";
            }

            RefreshZoneAnnotationList();
        }

        private void RefreshZoneAnnotationList()
        {
            zoneAnnotationBindingSource.Clear();
            zoneAnnotationBindingSource.DataSource = Cartographer.CurrentZoneAnnotations;
            zoneAnnotationBindingSource.ResetBindings(false);
        }

        private void check_ShowPlayerLocHistory_CheckedChanged(object sender, EventArgs e)
        {
            if (initialLoadCompleted)
                Cartographer.TogglePlayerLocationHistory(check_ShowPlayerLocHistory.Checked);
        }

        private void button_SetNoteCoordsToPlayerLoc_Click(object sender, EventArgs e)
        {
            SetNoteCoordsToPlayerLocation();
        }

        private void SetNoteCoordsToPlayerLocation()
        {
            if (Cartographer.PlayerCoords != null)
            {
                txt_NewNoteCoords.Text = $"{Cartographer.PlayerCoords.Y}, {Cartographer.PlayerCoords.X}";
            }
        }

        private void dgv_ZoneAnnotation_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (check_AutosaveNotes.Checked)
            {
                Cartographer.SaveNotes();
            }
        }

        private void button_NotesColor_Click(object sender, EventArgs e)
        {
            DialogResult result = colorDialog_Notes.ShowDialog();
            if (result == DialogResult.OK)
            {
                button_NotesColor.BackColor = colorDialog_Notes.Color;
                Cartographer.UpdateNotesColor(colorDialog_Notes.Color);
            }
        }

        private void button_NotesFont_Click(object sender, EventArgs e)
        {
            DialogResult result = fontDialog_Notes.ShowDialog();
            if (result == DialogResult.OK)
            {
                button_NotesFont.Font = fontDialog_Notes.Font;
                Cartographer.UpdateNotesFont(fontDialog_Notes.Font);
            }
        }

        private void check_ShowAnnotations_CheckStateChanged(object sender, EventArgs e)
        {
            if (initialLoadCompleted)
                Cartographer.ToggleZoneAnnotations((int)check_ShowAnnotations.CheckState);
        }

        private void timer_ParseLogsTimer_Tick(object sender, EventArgs e)
        {
            if (initialLoadCompleted)
                Cartographer.CheckLogParserForNewLines();
        }

        private void button_SaveNotes_Click(object sender, EventArgs e)
        {
            if (initialLoadCompleted)
                Cartographer.SaveNotes();
        }

        private void check_AutosaveNotes_CheckedChanged(object sender, EventArgs e)
        {

        }


        private void btn_EditMapCoords_Click(object sender, EventArgs e)
        {
            MapCoordEditor fixer = new MapCoordEditor(Cartographer);
            fixer.Show();
        }

        private void zoneAnnotationBindingSource_CurrentChanged(object sender, EventArgs e)
        {
            // Maybe more bindings but augh not right now
        }

        private void dgv_ZoneAnnotation_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Ignore clicks that are not on button cells. 
            if (e.RowIndex < 0 || e.ColumnIndex !=
                dgv_ZoneAnnotation.Columns["ColDeleteButton"].Index) return;

            // validate deletion target
            string note = dgv_ZoneAnnotation["Note", e.RowIndex].Value.ToString();
            string y = dgv_ZoneAnnotation["YCoord", e.RowIndex].Value.ToString();
            string x = dgv_ZoneAnnotation["XCoord", e.RowIndex].Value.ToString();
            string mapShortName = dgv_ZoneAnnotation["MapShortName", e.RowIndex].Value.ToString();
            string subMap = dgv_ZoneAnnotation["SubMap", e.RowIndex].Value.ToString();

            var match = Cartographer.CurrentZoneAnnotations.Where(za => za.Note == note
                && (za.X.ToString() == x)
                && (za.Y.ToString() == y)
                && (za.MapShortName == mapShortName)
                && (za.SubMap.ToString() == subMap)).FirstOrDefault();

            if (match != null)
            {
                DialogResult confirmDeleteNote = MessageBox.Show($"Delete note {note} in {mapShortName} at {y} {x} ? ", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirmDeleteNote == DialogResult.Yes)
                {
                    Cartographer.RemoveNote(match);
                }
            }

            RefreshZoneAnnotationList();
        }

        private void check_AutoParseFromLogs_CheckedChanged(object sender, EventArgs e)
        {
            Cartographer.ToggleAutoParse(check_AutoParseFromLogs.Checked);
        }

        private void nud_ParseTimerMS_ValueChanged(object sender, EventArgs e)
        {
            if (nud_ParseTimerMS.Value > 0)
            {
                Cartographer.SetParseTimerMS((int)nud_ParseTimerMS.Value);
                timer_ParseLogsTimer.Interval = Settings.AutoParseIntervalMS;
            }
        }
    }
}
