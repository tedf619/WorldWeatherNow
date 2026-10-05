namespace WorldWeatherNow;

/// <summary>
/// The Tile class and its derived classes represent a single tile in a tile-based map system, such as OpenStreetMap or NOAA weather overlays. 
/// All Tile coordinates are in EPSG:3857 (Web Mercator) format. Coordinates are expressed in meters, with the origin (0,0) corresponding to
/// latitude=0° and longitude= 0°.
/// In Web Mercator coordinates, the X-axis increases to the right (east) and the Y-axis increases upward (north).
/// Both axes run from −20,038 to +20,038 km, which corresponds to the circumference of the Earth at the equator. 
///
/// TLDR:
///   EPSG (European Petroleum Survey Group) is a defunct organization that originally defined a set of standard coordinate reference systems for 
///   geospatial data. EPSG published a series of standards, one of which became known as EPSG:3857.
///   EPSG:3857 is a coordinate reference system used for web mapping applications. It allows for efficient tile-based rendering of maps, 
///   based on the Mercator projection. EPSG:3857 is widely used by popular mapping services like Google Maps, OpenStreetMap, and Bing Maps.
///   
///   Note: EPSG:3857 is not a true representation of the Earth's surface. Being a Mercator projection, 3857 distorts areas and distances,
///   especially near the poles. Still, it is often used for web mapping due to its simplicity and compatibility with populartile-based systems.
/// </summary>
public class Tile
{
  const double MaxTileCoordinateValue = 20_037_508.342789244; // half the earth circumference at the equator (in meters)

  public string Name { get; set; } = string.Empty;
  public bool Visible { get; set; }
  public float Opacity { get; set; } = 1f;
  public int Version { get; set; }

  /// <summary>returns bounding box (in meters) of a tile in EPSG:3857 (Web Mercator) format.</summary>
  protected (double MinX, double MinY, double MaxX, double MaxY) GetTileBounds(int zoom, int x, int y)
  {
    double size = 2 * MaxTileCoordinateValue / (1 << zoom);  // size of each side of a tile in meters at the given zoom level
    double minX = -MaxTileCoordinateValue + x * size;        // left edge of the tile in meters
    double maxY = MaxTileCoordinateValue - y * size;         // top edge of the tile in meters
    return (minX, maxY - size, minX + size, maxY);
  }

  protected string F(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

  // the URI where the tile can be fetched from the server, given the zoom level and tile coordinates (x, y)
  public virtual async Task<string> GetUri(int zoom, int x, int y, int version = 0, MapStyle style = MapStyle.OpenStreetMap)
  {
    return "";
  }
}

/// <summary>OpenStreetMap standard tile.</summary>
public class TileMap : Tile
{
  public TileMap()
  {
    Name = "OpenStreetMap";
    Visible = true;
  }

  public override async Task<string> GetUri(int zoom, int x, int y, int version = 0, MapStyle style = MapStyle.OpenStreetMap)
  {
    return GetUriForStyle(zoom, y, x, style);
  }

  // get the URI that generates the tile bitmap, based on the map style
  string GetUriForStyle(int zoom, int tileY, int wrappedTileX, MapStyle style) => style switch
  {
    MapStyle.OpenStreetMap => $"https://tile.openstreetmap.org/{zoom}/{wrappedTileX}/{tileY}.png",
    MapStyle.WorldImagery => $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{zoom}/{tileY}/{wrappedTileX}",
    MapStyle.WorldPhysical => $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Physical_Map/MapServer/tile/{zoom}/{tileY}/{wrappedTileX}",
    MapStyle.NationalGeographic => $"https://server.arcgisonline.com/ArcGIS/rest/services/NatGeo_World_Map/MapServer/tile/{zoom}/{tileY}/{wrappedTileX}",
    MapStyle.DarkGrayCanvas => $"https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{zoom}/{tileY}/{wrappedTileX}",
    MapStyle.LightGrayCanvas => $"https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer/tile/{zoom}/{tileY}/{wrappedTileX}",
    _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported map style")
  };
}

/// <summary>Cloud data in EPSG:3857 (Web Mercator) format.</summary>
public class TileClouds : Tile
{
  const string EndPoint = "https://nowcoast.noaa.gov/geoserver/satellite/wms";
  const string Layer = "global_longwave_imagery_mosaic";

  public TileClouds(float opacity)
  {
    Name = "NOAA Clouds (GOES IR)";
    Opacity = opacity;
  }

  public override async Task<string> GetUri(int zoom, int x, int y, int version = 0, MapStyle style = MapStyle.OpenStreetMap)
  {
    var bounds = GetTileBounds(zoom, x, y);
    return $"{EndPoint}?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS={Layer}&STYLES=" +
             $"&CRS=EPSG:3857&BBOX={F(bounds.MinX)},{F(bounds.MinY)},{F(bounds.MaxX)},{F(bounds.MaxY)}" +
             $"&WIDTH=256&HEIGHT=256&FORMAT=image/png&TRANSPARENT=TRUE&_={version}";
  }
}

/// <summary>Radar tile in EPSG:3857 (Web Mercator) format.</summary>
public class TileRain : Tile
{
  static HttpClient? http = new();
  static string? localPath;

  public TileRain(float opacity)
  {
    Name = "RainViewer Precipitation";
    Opacity = opacity;
  }

  public async override Task<string> GetUri(int zoom, int x, int y, int version = 0, MapStyle style = MapStyle.OpenStreetMap)
  {
    if (localPath == null)
      localPath = await FetchRadarLocalPathAsync();

    return $"https://tilecache.rainviewer.com{localPath}/256/{zoom}/{x}/{y}/2/1_1.png";
  }

  async Task<string> FetchRadarLocalPathAsync()
  {
    string json = await http!.GetStringAsync("https://api.rainviewer.com/public/weather-maps.json");
    using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
    var root = doc.RootElement;

    if (root.TryGetProperty("radar", out var radar) &&
        radar.TryGetProperty("past", out var past) &&
        past.GetArrayLength() > 0)
    {
      int lastIdx = past.GetArrayLength() - 1;
      string? latestRadarPath = past[lastIdx].GetProperty("path").GetString();
      return latestRadarPath ?? string.Empty;
    }

    return string.Empty;
  }
}

/// <summary>Surface temperatures in EPSG:3857 (Web Mercator) format.</summary>
public class TileTemperaturesOnSurface : Tile
{
  const string EndPoint = "https://gibs.earthdata.nasa.gov/wmts/epsg3857/best";
  const string Layer = "MODIS_Terra_Land_Surface_Temp_Day";

  public TileTemperaturesOnSurface(float opacity)
  {
    Name = "NASA GIBS Surface Temperatures";
    Opacity = opacity;
  }

  public async override Task<string> GetUri(int zoom, int x, int y, int version = 0, MapStyle style = MapStyle.OpenStreetMap)
  {
    DateTime date = DateTime.UtcNow.AddHours(-4);  // realtime data not immediately available for free
    string dateStr = date.ToString("yyyy-MM-dd");
    string tileMatrixSet = "GoogleMapsCompatible_Level7";
    return $"{EndPoint}/{Layer}/default/{dateStr}/{tileMatrixSet}/{zoom}/{y}/{x}.png";
  }
}
