namespace WorldWeatherNow;

public partial class FormMain : Form
{
  public FormMain()
  {
    InitializeComponent();

    PopulateMapStyleComboBox();

    mapControl.CenterView(latitude:50, longitude:30, zoom:4); // continental Europe

    labelZoomLevel.Text = $"Zoom level: {mapControl.Zoom}";
    labelRefreshedAt.Text = $"Refreshed at {DateTime.Now.ToString("T")}";
  }

  public void PopulateMapStyleComboBox()
  {
    var items = Enum.GetValues<MapStyle>()
        .Select(e => new
        {
          Value = e,
          Description = e.GetDescription() // GetDescription is an extension method
        })
        .ToList();

    comboBoxMapStyle.DataSource = items;
    comboBoxMapStyle.DisplayMember = "Description";
    comboBoxMapStyle.ValueMember = "Value";
  }

  void ComboBoxMapStyle_SelectedIndexChanged(object sender, EventArgs e)
  {
    if (comboBoxMapStyle.SelectedValue is MapStyle style)
      mapControl.SetMapStyle(style);
  }

  void CheckBoxShowClouds_CheckedChanged(object sender, EventArgs e)
  {
    mapControl.ShowClouds(checkBoxShowClouds.Checked, trackBarOpacityClouds.Value / 100f);
  }

  void TrackBarOpacityClouds_Scroll(object sender, EventArgs e)
  {
    mapControl.ShowClouds(checkBoxShowClouds.Checked, trackBarOpacityClouds.Value / 100f);
  }

  void CheckBoxShowRain_CheckedChanged(object sender, EventArgs e)
  {
    panelColorsRain.Visible = checkBoxShowRain.Checked;
    mapControl.ShowRain(checkBoxShowRain.Checked, trackBarOpacityRain.Value / 100f);
  }

  void TrackBarOpacityRain_Scroll(object sender, EventArgs e)
  {
    mapControl.ShowRain(checkBoxShowRain.Checked, trackBarOpacityRain.Value / 100f);
  }

  void CheckBoxShowTemperatures_CheckedChanged(object sender, EventArgs e)
  {
    panelColorsTemperatures.Visible = checkBoxShowTemperatures.Checked;
    mapControl.ShowTemperatures(checkBoxShowTemperatures.Checked, trackBarOpacityTemperatures.Value / 100f);
  }

  void TrackBarOpacityTemperatures_Scroll(object sender, EventArgs e)
  {
    mapControl.ShowTemperatures(checkBoxShowTemperatures.Checked, trackBarOpacityTemperatures.Value / 100f);
  }

  void ButtonRefresh_Click(object sender, EventArgs e)
  {
    RefreshOverlays();
  }

  void TimerAutoRefresh_Tick(object sender, EventArgs e)
  {
    RefreshOverlays();
  }

  void MapControl_StatusChanged(string status)
  {
    labelStatusMessage.Text = status;
  }

  void MapControl_ZoomLevelChanged(int zoomLevel)
  {
    labelZoomLevel.Text = $"Zoom level: {zoomLevel}";
  }

  void RefreshOverlays()
  {
    mapControl.RefreshOverlays();
    labelRefreshedAt.Text = $"Refreshed at {DateTime.Now.ToString("T")}";
  }
}
