# WorldWeatherNow 

A minimalist .NET 10 WinForms application that displays current weather information for a any location in the world.

## Features
* Great project for getting started with maps and online weather services.
* Completely self-contained. No NuGet packages, third-party libraries or API keys needed. 
* Supports several popular map styles.
* Shows cloud cover, rain and temperatures worldwide.
* Uses free online services for all data.
    * Maps: OpenStreetMap and ArcGisOnline.
    * Cloud cover: NOAA (National Oceanic and Atmospheric Administration).
    * Rain: RainViewer.
    * Temperatures: NASA.
<br><br>

This app shows a map of any area in the world, with optional overlays for cloud cover, rain and temperatures. The following figure shows WorldWeatherNow in action, with the OpenStreetMap style selected.

<img width="1161" height="707" alt="image" src="https://github.com/user-attachments/assets/f6cf7e9a-5eb0-46a5-a27d-50c0852d0a69" />

*Figure 1* - The app showing where rain is present.
<br>

When you select the Temperatures overlay, a handy color legend pops up to indicate the severity of rain.
<p></p>
To get a better idea of the developing weather, you can turn on the cloud cover layer, as shown in the next figure.
<br><br>

<img width="1161" height="707" alt="image" src="https://github.com/user-attachments/assets/9e01b1d4-0c87-47c7-8fd5-4b3ab497df18" />

*Figure 2* - The app showing both clouds and rain.

The cloud data is retrieved from NOAA's **nowcast** server at ```https://nowcoast.noaa.gov/geoserver/satellite/wms```.
The rain data is from RainViewer's **tilecache** server at ```https://tilecache.rainviewer.com```.

You can also view ground temperatures, as shown in the next figure.

<img width="1161" height="707" alt="image" src="https://github.com/user-attachments/assets/3734bf08-aaa7-454f-b3a2-b10530ebd85b" />

*Figure 3* - The app showing ground temperatures.
 
The temperature data is from NASA's **GIBS** server at ```https://gibs.earthdata.nasa.gov```. When you select the Temperatures overlay, another handy color legend pops up. Note that some areas may show no temperature color. This can happen for two reasons: 
  1. The area is obscured by clouds.
  2. NASA hasn't posted the latest data yet for that location.

The data isn't posted in realtime, so we request data from 4 hours ago.
<p></p>
By default, the app uses OpenStreetMap for the map, but several other map styles are supported including satellite views, National Geographic maps and others. The following figure shows some examples.
<br><br>
<img width="1161" height="707" alt="image" src="https://github.com/user-attachments/assets/4c41927f-e1cf-47b9-90dc-a9dcb5768c54" />

*Figure 4* - The satellite view.
<br><br>

<img width="1161" height="707" alt="image" src="https://github.com/user-attachments/assets/284d2fa6-c5c2-44b2-8ea8-3658e7495330" />

*Figure 5* - The *National Geographic* map style.
<br><br>

<img width="1161" height="707" alt="image" src="https://github.com/user-attachments/assets/0fc4cf9b-6063-4d48-b720-f4b28e6c7886" />

*Figure 6* - The physical map style.



## The UI Layout

The user interface uses two layers of docked panels for its layout, as shown in the following figure.

<img width="723" height="521" alt="image" src="https://github.com/user-attachments/assets/409f51ba-ed38-461f-9c2d-1a299f02a780" />

*Figure 7* - The docked panels for the user interface.

* *Layer 1* - Shown in red, this layer divides the screen vertically, with the middle area filling available space.
* *Layer 2* - Shown in blue, this layer is just for the map, which fills the middle area of layer 1.

To achieve the required layout, the panels in layer 1 must be added in the proper back-to-front order, like this:

  1. PanelTop.
  2. PanelColorsRain.
  3. PanelColorsTemperatures.
  4. PanelBottom.
  5. PanelMiddle.

As a general rule, components with Dock=Fill must always be added last to their parent container.

## How it Works

The main form of the app is just used to setup the layout and handle user interactions with the UI controls. The interesting stuff is
mostly in MapControl. It handles the download and display of the map, the overlays for cloud cover, rain and temperatures. All four of these are made up by tiles that fit tightly together to create a complete and seamless image.
The geographic area covered by each tile depends on the Zoom level, which you can change using the mouse wheel. You can also pan the map by clicking and dragging the mouse.
<p>
The map is initialized in FormMain with the following code fragment.

```csharp
public partial class FormMain : Form
{
  public FormMain()
  {
    InitializeComponent();

    //...
    mapControl.CenterView(latitude:50, longitude:30, zoom:4); // continental Europe
  }
}
```
*Listing 1* - Initializing the map.

The map is centered on latitude=50 and longitude=30, with zoom=4. These values make the map show continental Europe at startup time. Regardless of where the map starts up, you can always zoom and pan to any other place on the globe. 
<p>
FormMain contains no other interesting logic, other than handling user clicks and mouse actions, which are all delegated to MapControl. When you drag the map, MapControl downloads tiles for new areas being shown. When you zoom the map, MapControl downloads new tiles for the new zoom level. Tiles are stored in a memory cache, to obviate the need to download a given tile more than once.

## The Map

Displaying a map is more complicated than it might seem at first, because the Earth is round and we usually display maps on a flat surface. A rather naive but simple way to handle the problem is to use a Mercator projection of the Earth.
The idea is to conceptually wrap a cylinder around the Earth so that it touches the globe at the equator. Mercator maps are the projection of the Earth's surface onto that cylinder, which is then unwrapped into a flat rectangle. The following figure shows the idea as described by ChatGPT.

<img width="622" height="491" alt="image" src="https://github.com/user-attachments/assets/fac56686-6734-4c17-8964-92ee0bb79fe0" />

*Figure 8* - Generating a Mercator projection of the Earth.

Mercator projections provide reasonably good approximations of countries at lower latitudes. The farther you get from the equator, the greater the distortion. Near the poles, the distortion is huge. Look at you big Greenland and Antarctica appear on the map. What makes Mercator projections popular is their ease of use. You can simply divide the square map into smaller square tiles, avoiding a lot of tricky math to account for the round Earth.
<br><br>
The *Web Mercator* is a special format for Mercator maps. It's based on a standard called EPSG:3857, which uses 256x256 pixel tiles. The coordinate system uses longitude for the X axis and latitude for the Y axis. The origin corresponds to the point at longitude=0 and latitude=0, which is the center of the map. This point turns out to be somewhere in the Gulf of Guinea, off the coast of West Africa. The X axis increases to the right (East). The Y axis increases upward (North).
In Web Mercator, the Earth is assumed to be a perfect sphere, so the height and width of the map are the same: about 40,000 km, which is the circumference of the Earth at the equator. Since the origin is in the middle of the map, no location can be farther than about + or - 20,000 km.

## Tiles

As mentioned earlier, the Web Mercator format is based on 256x256 pixel tiles. WorldWeatherNow uses tiles for four things:

  1. The map.
  2. The cloud layer.
  3. The rain layer.
  4. The temperatures layer.

The main difference between the four tiles is their name and the URI of the webservice endpoint that supplies their bitmaps. The following figure shows class hierarchy.

<img width="890" height="224" alt="image" src="https://github.com/user-attachments/assets/16a6ed11-4409-4397-8429-05ee76fc6e81" />

*Figure 9* - The Tile class hierachy.

The tile name is set in the class constructors. The GetUri method is virtual, so calling tile.GetUri will polymorphically call the GetUri method of whatever tile type you have.
The following listing shows the highlights of the Tile classes.

```csharp
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
```

*Listing 2* - The code highlights of the Tile classes.

## Downloading Tiles

MapControl handles the downloading of all tiles using the RequestTile method. The following listing shows the main code.

```csharp
  async void RequestTile(Tile tile, int zoom, int x, int y, string cacheKey)
  {
    // download the bytes for a tile from the server for the given tile type
    byte[] data = await http.GetByteArrayAsync(tile.GetUri(zoom, x, y, tile.Version));  // Tile.GetUri() is virtual

    // convert bytes to a bitmap representation of the tile
    using var ms = new MemoryStream(data);
    var bitmap = new Bitmap(ms);

    // save tile in the cache
    tileCache[cacheKey] = bitmap;
    tileOrderInCache.Enqueue(cacheKey);

    Invalidate();
  }
```

*Listing 3* - Downloading tiles.

The first line calls Tile.GetUri(...) to get the URI for the tile. Since this method is virtual, it returns the correct URI based on the type of Tile involved. Downloaded tiles are stored in a tile cache, which is just a Dictionary defined like this: ```Dictionary<string, Bitmap>```. The value is the 256x256 pixel bitmap for the tile, the key is a combination of tile-specific properties put together with the following code:

```csharp
string GetCacheKey(Tile tile, int zoom, int x, int y, MapStyle style) => $"{tile.Name}|{tile.Version}|{zoom}|{x}|{y}|{style}";
```
*Listing 4* - Generating a key for the Tile cache.

Before calling RequestTile, MapControl checks to see if the tile is already in the cache. If so, it uses the cached tile.

## Painting Tiles

Painting a map on the screen entails painting tiles. These are painted in the method MapControl.OnPaint, whose highlights are shown in the following listing.

```csharp
protected override void OnPaint(PaintEventArgs e)
{
  var g = e.Graphics;
  //...
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
    //...
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

        // if the tile is already downloaded and cached, draw it; otherwise, request it

        if (tileCache.TryGetValue(key, out var bitmap))
          g.DrawImage(bitmap, destination, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, imageAttributes);

        else if (!failedTiles.Contains(key))
          RequestTile(layer, Zoom, wrappedX, ty, key);
      }
    }
  }
}
```

*Listing 5* - Painting tiles on the screen.

Tile coordinates are in meters, indicating the X and Y distance from the center of the Web Mercator map. To paint tiles, these coordinates must be converted to MapControl client coordinates. 
The line ```g.DrawImage(...)``` does the actual painting.

## Closing Notes

Much of the logic in a mapping app is devoted to coordinate management and conversions. If you don't understand the coordinate systems, you'll have a hard time modifying the code to add your own new features.
A few features that could be useful are:

1. Showing overlays of other Earth resources. For example ocean temperatures, agriculture data, dust and pollution, deforestation and much more.
   <br><br>
2. Animation. Seeing a static image of the weather doesn't tell you the whole picture. What is more interesting is how the weather is moving and changing. This requires animation, showing a series of frames over time up to the present. Handling animation isn't trivial, so I omitted it in this app.

Many thanks to Google Gemini and ChatGPT.

