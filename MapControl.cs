using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WorldWeatherNow;

public partial class MapControl : UserControl
{
  const int TileSizeInPixels = 256;  // size of each side of our square tiles in pixels
  const int MaxCachedTiles = 900;

  readonly HttpClient http;
  readonly SemaphoreSlim tileDownloadLimiter = new(8);    // so we don't download more than 8 tiles at once
  readonly Dictionary<string, Bitmap> tileCache = new();  // so we don't request tiles already downloaded
  readonly Queue<string> tileOrderInCache = new();        // used to removed oldest tiles when cache is full
  readonly HashSet<string> pendingTiles = new();          // so we don't request the same tile multiple times at once
  readonly HashSet<string> failedTiles = new();           // so we don't keep requesting tiles that failed to download  
  readonly Tile layerMap = new TileMap();
  readonly Tile layerClouds = new TileClouds(0.55f);
  readonly Tile layerRain = new TileRain(0.75f);
  readonly Tile layerTemperaturesOnSurface = new TileTemperaturesOnSurface(0.75f);

  Point mouseDragStart;
  bool isDraggingMouse;
  double centerX, centerY; // world pixel coords at current zoom
  MapStyle mapStyle;

  public List<Tile> Layers { get; } = new();

  public int Zoom { get; private set; } = 4;

  [DefaultValue(0)]
  public int MinZoom { get; set; } = 3;

  [DefaultValue(0)]
  public int MaxZoom { get; set; } = 14;

  public MapControl()
  {
    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    BackColor = Color.FromArgb(170, 211, 223);
    Cursor = Cursors.Hand;

    http = new HttpClient();
    http.Timeout = TimeSpan.FromSeconds(20);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherMapWinForms/1.0 (sample app)");

    // add layers in back-to-front order
    Layers.AddRange(new[] { layerMap, layerClouds, layerTemperaturesOnSurface, layerRain });
  }

  public void SetMapStyle(MapStyle style)
  {
    mapStyle = style;
    Invalidate();
  }

  public void ShowClouds(bool show, float opacity)
  {
    layerClouds.Visible = show;
    layerClouds.Opacity = opacity;
    Invalidate();
  }

  public void ShowRain(bool show, float opacity)
  {
    layerRain.Visible = show;
    layerRain.Opacity = opacity;
    Invalidate();
  }

  public void ShowTemperatures(bool show, float opacity)
  {
    layerTemperaturesOnSurface.Visible = show;
    layerTemperaturesOnSurface.Opacity = opacity;
    Invalidate();
  }

  string GetCacheKey(Tile tile, int zoom, int x, int y, MapStyle style) => $"{tile.Name}|{tile.Version}|{zoom}|{x}|{y}|{style}";

  /// <summary>
  /// Calculates the size of each side of the square world Mercator map in pixels at a given zoom level.
  /// At zoom level 0, the world is 256x256 pixels, so it fits in a single tile.
  /// Each increase in zoom level doubles the size of the world's side dimension (and quadrupling the area).
  /// </summary>
  double WorldSize(int zoom) => TileSizeInPixels * Math.Pow(2, zoom);

  /// <summary>
  /// Converts latitude and longitude to world pixel coordinates at a given zoom level.
  /// Assumes the world is a square Mercator map of size WorldSize(zoom) x WorldSize(zoom) pixels, 
  /// with (0,0) at the top-left corner and (WorldSize(zoom), WorldSize(zoom)) at the bottom-right corner.
  /// </summary>
  public (double X, double Y) LatLonToWorld(double lat, double lon, int zoom)
  {
    double size = WorldSize(zoom);
    double x = (lon + 180.0) / 360.0 * size;
    double sin = Math.Sin(lat * Math.PI / 180.0);
    double y = (0.5 - Math.Log((1 + sin) / (1 - sin)) / (4 * Math.PI)) * size;
    return (x, y);
  }

  /// <summary>
  /// Converts world pixel coordinates to latitude and longitude at a given zoom level.
  /// Like LatLonToWorld(), it assumes the world is a square Mercator map of size WorldSize(zoom) x WorldSize(zoom) pixels, 
  /// with (0,0) at the top-left corner and (WorldSize(zoom), WorldSize(zoom)) at the bottom-right corner.
  /// </summary>
  public (double Lat, double Lon) WorldToLatLon(double x, double y, int zoom)
  {
    double size = WorldSize(zoom);
    double lon = x / size * 360.0 - 180.0;
    double n = Math.PI - 2.0 * Math.PI * y / size;
    double lat = 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
    return (lat, lon);
  }

  /// <summary>
  /// Centers the view of the map on a specific latitude, longitude, and zoom level.
  /// </summary>
  public void CenterView(double latitude, double longitude, int zoom)
  {
    Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
    (centerX, centerY) = LatLonToWorld(latitude, longitude, Zoom);
    ClampCenter();
    Invalidate();
  }

  /// <summary>
  /// Zooms the map in or out by a specified delta, optionally around a specific point.
  /// </summary>
  public void ZoomBy(int delta, Point? anchor = null)
  {
    int newZoom = Math.Clamp(Zoom + delta, MinZoom, MaxZoom);
    if (newZoom == Zoom) return;

    Point a = anchor ?? new Point(Width / 2, Height / 2);

    // keep the geographic point under the anchor fixed while zooming
    var (lat, lon) = WorldToLatLon(centerX - Width / 2.0 + a.X, centerY - Height / 2.0 + a.Y, Zoom);
    Zoom = newZoom;
    var (wx, wy) = LatLonToWorld(lat, lon, Zoom);
    centerX = wx - a.X + Width / 2.0;
    centerY = wy - a.Y + Height / 2.0;
    ClampCenter();
    Invalidate();
    FireZoomLevelChanged(Zoom);
  }

  /// <summary>
  /// Restricts the center coordinates within the valid range of the world map at the current zoom level.
  /// </summary>
  void ClampCenter()
  {
    double size = WorldSize(Zoom);
    centerY = Math.Clamp(centerY, Height / 2.0 - 0, Math.Max(Height / 2.0, size - Height / 2.0));
    centerX = ((centerX % size) + size) % size;
  }

  public void RefreshOverlays()
  {
    RefreshLayer(layerClouds);
    RefreshLayer(layerRain);
    RefreshLayer(layerTemperaturesOnSurface);
  }

  public void RefreshLayer(Tile layer)
  {
    layer.Version++;  // so we don't try to use cached tiles for the overlay tiles
    Invalidate();
  }

  /// <summary>
  /// Requests a tile from the server for a given zoom level and world coordinates.
  /// </summary>
  async void RequestTile(Tile tile, int zoom, int x, int y, string cacheKey)
  {
    if (!pendingTiles.Add(cacheKey)) return;  // already requested

    try
    {
      await tileDownloadLimiter.WaitAsync();  // so we don't let too many downloads overlap
      try
      {
        FireStatusChanged("Downloading tiles...");
        UseWaitCursor = true;

        // download the bytes for a tile from the server for the given tile type
        byte[] data = await http.GetByteArrayAsync(await tile.GetUri(zoom, x, y, tile.Version, mapStyle));  // Tile.GetUri() is virtual

        FireStatusChanged("");
        UseWaitCursor = false;

        // convert bytes to a bitmap representation of the tile
        using var ms = new MemoryStream(data);
        var bitmap = new Bitmap(ms);

        // save tile in the cache
        tileCache[cacheKey] = bitmap;
        tileOrderInCache.Enqueue(cacheKey);

        // don't let cache get too big
        while (tileOrderInCache.Count > MaxCachedTiles)
        {
          // remove the oldest tile from the cache and dispose of its bitmap
          string oldestTileKey = tileOrderInCache.Dequeue();

          if (tileCache.Remove(oldestTileKey, out var oldestTile))
            oldestTile.Dispose();
        }
        Invalidate();
      }
      finally
      {
        tileDownloadLimiter.Release();
      }
    }
    catch (Exception ex)
    {
      failedTiles.Add(cacheKey);

      string text = $"Tile error ({tile.Name}): {ex.Message}";
      FireStatusChanged(text);
    }
    finally
    {
      pendingTiles.Remove(cacheKey);
    }
  }

  protected override void OnPaint(PaintEventArgs e)
  {
    var g = e.Graphics;
    g.Clear(GetBackgroundColor(mapStyle));
    g.InterpolationMode = InterpolationMode.HighQualityBilinear;

    double size = WorldSize(Zoom);  // size of each side in pixels of the square Mercator world at this zoom level
    int tilesPerSide = 1 << Zoom;   // number of tiles per side of the square Mercator map at this zoom level
    double left = centerX - Width / 2.0;
    double top = centerY - Height / 2.0;

    int x0 = (int)Math.Floor(left / TileSizeInPixels);                                       // leftmost tile index (can be negative)
    int x1 = (int)Math.Floor((left + Width) / TileSizeInPixels);                             // rightmost tile index (can be greater than tilesPerSide - 1)
    int y0 = Math.Max(0, (int)Math.Floor(top / TileSizeInPixels));                           // topmost tile index (clamped to 0)
    int y1 = Math.Min(tilesPerSide - 1, (int)Math.Floor((top + Height) / TileSizeInPixels)); // bottommost tile index (clamped to tilesPerSide - 1)

    foreach (var layer in Layers.Where(l => l.Visible))
    {
      using var imageAttributes = new ImageAttributes();
      if (layer.Opacity < 0.999f)
      {
        var cm = new ColorMatrix { Matrix33 = layer.Opacity };
        imageAttributes.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
      }

      for (int ty = y0; ty <= y1; ty++)     // iterate over the visible tile rows
      {
        for (int tx = x0; tx <= x1; tx++)   // iterate over the visible tile columns
        {
          int wrappedX = ((tx % tilesPerSide) + tilesPerSide) % tilesPerSide;  // wrap X coordinate around the world map
          string key = GetCacheKey(layer, Zoom, wrappedX, ty, mapStyle);       // unique key for this tile in the cache

          var destination = new Rectangle(                    // client coordinates where the tile should be drawn
              (int)Math.Round(tx * TileSizeInPixels - left),  // top-left X of the tile in client coordinates (0,0 is top-left of control)
              (int)Math.Round(ty * TileSizeInPixels - top),   // top-left Y of the tile in client coordinates
              TileSizeInPixels, TileSizeInPixels);            // width and height of the tile in client coordinates

          // if the tile is already downloaded and cached, draw it; otherwise download it

          if (tileCache.TryGetValue(key, out var bitmap))
            g.DrawImage(bitmap, destination, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, imageAttributes);

          else if (!failedTiles.Contains(key))
            RequestTile(layer, Zoom, wrappedX, ty, key);
        }
      }
    }

    DrawCredits(g);
  }

  /// <summary>
  /// Shows credits for the map and overlay layers in the bottom-right corner of the map.
  /// </summary>
  /// <param name="g"></param>
  void DrawCredits(Graphics g)
  {
    string text = "Maps: " + GetMapCredits(mapStyle);
    if (layerClouds.Visible) text += " | Clouds: NOAA";
    if (layerRain.Visible) text += " | Rain: RainViewer";
    if (layerTemperaturesOnSurface.Visible) text += " | Temperatures: NASA GIBS";

    using var font = new Font("Segoe UI", 8f);
    var sz = g.MeasureString(text, font);
    var r = new RectangleF(Width - sz.Width - 6, Height - sz.Height - 4, sz.Width + 4, sz.Height);
    using var bg = new SolidBrush(Color.FromArgb(190, Color.White));
    g.FillRectangle(bg, r);
    g.DrawString(text, font, Brushes.Black, r.X + 2, r.Y + 1);
  }

  string GetMapCredits(MapStyle style) => style switch
  {
    MapStyle.OpenStreetMap => "OpenStreetMap",
    MapStyle.WorldImagery => "Esri, Maxar, Earthstar Geographics",
    MapStyle.WorldPhysical => "Esri, GEBCO, NOAA, National Geographic, DeLorme, HERE, Geonames.org, and other contributors",
    MapStyle.NationalGeographic => "National Geographic, Esri, Garmin, HERE, UNEP-WCMC, USGS, NASA, ESA, METI, NRCAN, GEBCO, NOAA, increment P Corp.",
    MapStyle.DarkGrayCanvas or
    MapStyle.LightGrayCanvas => "Esri, HERE, Garmin, © OpenStreetMap contributors, and the GIS user community",
    _ => ""
  };

  Color GetBackgroundColor(MapStyle style) => style switch
  {
    MapStyle.OpenStreetMap => Color.FromArgb(170, 211, 223),      // Light Blue
    MapStyle.WorldImagery => Color.FromArgb(10, 24, 42),          // Dark Ocean Blue
    MapStyle.WorldPhysical => Color.FromArgb(10, 24, 42),         // Dark Ocean Blue
    MapStyle.NationalGeographic => Color.FromArgb(238, 232, 218), // Soft Parchment
    MapStyle.DarkGrayCanvas => Color.FromArgb(33, 33, 33),        // Charcoal
    MapStyle.LightGrayCanvas => Color.FromArgb(210, 210, 210),    // Light Slate
    _ => Color.White
  };

  protected override void OnMouseDown(MouseEventArgs e)
  {
    if (e.Button == MouseButtons.Left)
    {
      isDraggingMouse = true;
      mouseDragStart = e.Location;
      Cursor = Cursors.SizeAll;
    }
    base.OnMouseDown(e);
  }

  protected override void OnMouseMove(MouseEventArgs e)
  {
    if (isDraggingMouse)
    {
      centerX -= e.X - mouseDragStart.X;
      centerY -= e.Y - mouseDragStart.Y;
      mouseDragStart = e.Location;
      ClampCenter();
      Invalidate();
    }
    var (lat, lon) = WorldToLatLon(centerX - Width / 2.0 + e.X, centerY - Height / 2.0 + e.Y, Zoom);
    FireStatusChanged($"Latitude: {lat:F4},  Longitude: {lon:F4}");
    base.OnMouseMove(e);
  }

  protected override void OnMouseUp(MouseEventArgs e)
  {
    isDraggingMouse = false;
    Cursor = Cursors.Hand;
    base.OnMouseUp(e);
  }

  protected override void OnMouseWheel(MouseEventArgs e)
  {
    ZoomBy(e.Delta > 0 ? 1 : -1, e.Location);
    base.OnMouseWheel(e);
  }

  protected override void OnMouseDoubleClick(MouseEventArgs e)
  {
    ZoomBy(1, e.Location);
    base.OnMouseDoubleClick(e);
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      foreach (var b in tileCache.Values) b.Dispose();
      tileCache.Clear();
    }
    base.Dispose(disposing);
  }

  #region Events
  public delegate void TextHandler(string text);
  public event TextHandler? StatusChanged;
  void FireStatusChanged(string text)
  {
    StatusChanged?.Invoke(text);
  }

  public delegate void IntHandler(int value);
  public event IntHandler? ZoomLevelChanged;
  void FireZoomLevelChanged(int value)
  {
    ZoomLevelChanged?.Invoke(value);
  }
  #endregion
}
