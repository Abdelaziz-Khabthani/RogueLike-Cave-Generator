using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour {
    
    [Header("Map Size")]
    public int width = 64;
    public int height = 64;
    public int mapBorderSize = 5;
    [Range(0f, 100f)]
    public int fillPercent = 47;

    [Header("Randomness")]
    public string seed;
    public bool useRandomSeed = true;

    [Header("Smoothness")]
    public int smoothIterations = 15;
    public int deathThreashold = 4;
    public int birthThreashold = 4;
    public bool selfSmoothing = true;
    public bool enablePostCleaing = true;
    public int postCleaningIterations = 1;
    public int postCleaningThreshold = 5;

    [Header("Cleaning Threshhold")]
    public int wallThresholdSize = 50;
    public int groundThresholdSize = 50;

    [Header("Tiles")]
    public Tilemap wallTileMap;
    public Tilemap groundTileMap;
    public RuleTile wallTile;
    public RuleTile groundTile;

    [Header("Rooms")]
    public bool connectRooms = true;
    [Range(1, 10)]
    public int passageRadius = 2;

    [Header("Debug")]
    public bool enableDebug = true;
    public Texture2D wallTexture;
    public Texture2D groundTexture;

    private int[,] map;
    private Map mapObj;

    public Map generateMap() {
        map = new int[width, height];
        fillMap();
        for (int i = 0; i < smoothIterations; i++) {
            if (selfSmoothing) {
                smoothMap();
            } else {
                map = smoothMap(map);
            }
        }
        cleanUpWallRegions();
        List<Room> rooms = cleanUpAndgetRooms();
        if (connectRooms) {
            detectMainRoom(rooms);
            connectClosestRooms(rooms, true);
        }
        if (enablePostCleaing) {
            for (int i = 0; i < postCleaningIterations; i++) {
                postClean();
            }
        }
        map = addBorders();
        mapObj = new Map(map, rooms);
        renderMap();
        return mapObj;
    }

    private void postClean() {
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        for (int i = 0; i < realWidth; i++) {
            for (int j = 0; j < realHeight; j++) {
                if (map[i, j] == Types.WALL) {
                    int numberOfGroundNeighbors = getNumberOfGroundNeighbors(i, j);
                    if (numberOfGroundNeighbors >= postCleaningThreshold) {
                        map[i, j] = Types.GROUND;
                    }
                }
            }
        }
    }

    private void renderMap() {
        wallTileMap.ClearAllTiles();
        //groundTileMap.ClearAllTiles();
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        for (int i = 0; i < realWidth; i++) {
            for (int j = 0; j < realHeight; j++) {
                if(map[i,j] == Types.WALL || map[i, j] == Types.EDGE) {
                    wallTileMap.SetTile(new Vector3Int(-realWidth / 2 + i, -realHeight / 2 + j, 0), wallTile);
                } else {
                    //groundTileMap.SetTile(new Vector3Int(-realWidth / 2 + i, -realHeight / 2 + j, 0), groundTile);
                }
            }
        }
    }

    private void detectMainRoom(List<Room> rooms) {
        rooms.Sort();
        rooms[0].isMainRoom = true;
        rooms[0].isAccessibleFromMainRoom = true;
    }

    private void connectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom) {
        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();
        if (forceAccessibilityFromMainRoom) {
            foreach (Room room in allRooms) {
                if (room.isAccessibleFromMainRoom) {
                    roomListB.Add(room);
                } else {
                    roomListA.Add(room);
                }
            }
        } else {
            roomListA = allRooms;
            roomListB = allRooms;
        }
        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;
        foreach (Room roomA in roomListA) {
            if (!forceAccessibilityFromMainRoom) {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0) {
                    continue;
                }
            }
            foreach (Room roomB in roomListB) {
                if (roomA == roomB || roomA.IsConnected(roomB)) {
                    continue;
                }
                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++) {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++) {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

                        if (distanceBetweenRooms < bestDistance || !possibleConnectionFound) {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }
            if (possibleConnectionFound && !forceAccessibilityFromMainRoom) {
                createPassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }
        if (possibleConnectionFound && forceAccessibilityFromMainRoom) {
            createPassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            connectClosestRooms(allRooms, forceAccessibilityFromMainRoom);
        }

        if (possibleConnectionFound && !forceAccessibilityFromMainRoom) {
            connectClosestRooms(allRooms, forceAccessibilityFromMainRoom);
        }
    }

    private void createPassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
        Room.ConnectRooms(roomA, roomB);
        List<Coord> line = getLine(tileA, tileB);
        foreach (Coord c in line) {
            drawCircle(c, passageRadius);
        }
    }

    private void drawCircle(Coord c, int r) {
        for (int x = -r; x <= r; x++) {
            for (int y = -r; y <= r; y++) {
                if (x * x + y * y <= r * r) {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if (isInMapRange(drawX, drawY)) {
                        map[drawX, drawY] = Types.GROUND;
                    }
                }
            }
        }
    }

    private bool isInMapRange(int x, int y) {
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        return x >= 0 && x < realWidth && y >= 0 && y < realHeight;
    }

    private List<Coord> getLine(Coord from, Coord to) {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest) {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++) {
            line.Add(new Coord(x, y));

            if (inverted) {
                y += step;
            } else {
                x += step;
            }

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest) {
                if (inverted) {
                    x += gradientStep;
                } else {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    private List<List<Coord>> cleanUpWallRegions() {
        List<List<Coord>> finalWallRegions = new List<List<Coord>>();
        List<List<Coord>> wallRegions = getRegions(Types.WALL);
        foreach (List<Coord> wallRegion in wallRegions) {
            if (wallRegion.Count < wallThresholdSize) {
                foreach (Coord tile in wallRegion) {
                    map[tile.tileX, tile.tileY] = 0;
                }
            }
        }
        return finalWallRegions;
    }

    private List<Room> cleanUpAndgetRooms() {
        List<Room> finalRooms = new List<Room>();
        List<List<Coord>> groundRegions = getRegions(Types.GROUND);
        foreach (List<Coord> groundRegion in groundRegions) {
            if (groundRegion.Count >= groundThresholdSize) {
                finalRooms.Add(new Room(groundRegion, map));
            } else {
                foreach (Coord tile in groundRegion) {
                    map[tile.tileX, tile.tileY] = 1;
                }
            }
        }
        return finalRooms;
    }

    private List<List<Coord>> getRegions(int tileType) {
        List<List<Coord>> regions = new List<List<Coord>>();
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        int[,] mapFlags = new int[realWidth, realHeight];
        for (int i = 0; i < realWidth; i++) {
            for (int j = 0; j < realHeight; j++) {
                if (mapFlags[i, j] == 0 && map[i, j] == tileType) {
                    List<Coord> newRegion = getRegionTiles(i, j);
                    regions.Add(newRegion);
                    foreach (Coord tile in newRegion) {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }
        return regions;
    }

    private List<Coord> getRegionTiles(int startX, int startY) {
        List<Coord> tiles = new List<Coord>();
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        int[,] mapFlags = new int[realWidth, realHeight];
        int tileType = map[startX, startY];
        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;
        while (queue.Count > 0) {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);
            for (int i = tile.tileX - 1; i <= tile.tileX + 1; i++) {
                for (int j = tile.tileY - 1; j <= tile.tileY + 1; j++) {
                    if (isInMapRange(i, j) && (j == tile.tileY || i == tile.tileX)) {
                        if (mapFlags[i, j] == 0 && map[i, j] == tileType) {
                            mapFlags[i, j] = 1;
                            queue.Enqueue(new Coord(i, j));
                        }
                    }
                }
            }
        }
        return tiles;
    }

    private int[,] addBorders() {
        int[,] borderedMap = new int[width + mapBorderSize * 2, height + mapBorderSize * 2];

        int realWidth = borderedMap.GetLength(0);
        int realHeight = borderedMap.GetLength(1);

        for (int i = 0; i < realWidth; i++) {
            for (int j = 0; j < realHeight; j++) {
                if (i >= mapBorderSize && i < width + mapBorderSize && j >= mapBorderSize && j < height + mapBorderSize) {
                    borderedMap[i, j] = map[i - mapBorderSize, j - mapBorderSize];
                } else {
                    borderedMap[i, j] = 1;
                }
            }
        }
        return borderedMap;
    }

    private void fillMap() {
        System.Random pseudoRandomGenerator = getPseudonRansomGenerator();
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        for (int i = 0; i < realWidth; i++) {
            for (int j = 0; j < realHeight; j++) {
                if (i == 0 || i == realWidth - 1 || j == 0 || j == realHeight - 1) {
                    map[i, j] = Types.WALL;
                } else {
                    map[i, j] = (pseudoRandomGenerator.Next(0, 100) < fillPercent) ? Types.WALL : Types.GROUND;
                }
            }
        }
    }

    private void smoothMap() {
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        for (int i = 0; i < realWidth; i++) {
            for (int j = 0; j < realHeight; j++) {
                int numberOfWallNeighbors = getNumberOfWallNeighbors(i, j);
                if (map[i, j] == Types.GROUND) {
                    if (numberOfWallNeighbors > birthThreashold) {
                        map[i, j] = Types.WALL;
                    }
                } else if (map[i, j] == Types.WALL) {
                    if (numberOfWallNeighbors < deathThreashold) {
                        map[i, j] = Types.GROUND;
                    }
                }
            }
        }
    }

    private int[,] smoothMap(int[,] oldMap) {
        int realWidth = map.GetLength(0);
        int realHeight = map.GetLength(1);
        int[,] newMap = new int[realWidth, realHeight];
        for (int i = 0; i < realWidth; i++) {
            for (int j = 0; j < realHeight; j++) {
                int numberOfWallNeighbors = getNumberOfWallNeighbors(i, j);
                if (oldMap[i, j] == Types.GROUND) {
                    if (numberOfWallNeighbors > birthThreashold) {
                        newMap[i, j] = Types.WALL;
                    } else {
                        newMap[i, j] = Types.GROUND;
                    }
                } else if (oldMap[i, j] == Types.WALL) {
                    if (numberOfWallNeighbors < deathThreashold) {
                        newMap[i, j] = Types.GROUND;
                    } else {
                        newMap[i, j] = Types.WALL;
                    }
                }
            }
        }
        return newMap;
    }

    private int getNumberOfGroundNeighbors(int posX, int posY) {
        int neighbors = 0;
        for (int i = posX - 1; i <= posX + 1; i++) {
            for (int j = posY - 1; j <= posY + 1; j++) {
                if (isInMapRange(i, j)) {
                    if (!(i == posX && j == posY) && (map[i, j] == Types.GROUND)) {
                        neighbors++;
                    }
                }
            }
        }
        return neighbors;
    }

    private int getNumberOfWallNeighbors(int posX, int posY) {
        int neighbors = 0;
        for (int i = posX - 1; i <= posX + 1; i++) {
            for (int j = posY - 1; j <= posY + 1; j++) {
                if (isInMapRange(i, j)) {
                    if (!(i == posX && j == posY) && map[i, j] == Types.WALL) {
                        neighbors++;
                    }
                } else {
                    neighbors++;
                }
            }
        }
        return neighbors;
    }

    private System.Random getPseudonRansomGenerator() {
        if (useRandomSeed) {
            seed = Time.time.ToString();
        }
        int intSeed = seed.GetHashCode();
        System.Random pseudoRandomGenerator = new System.Random(intSeed);
        return pseudoRandomGenerator;
    }

    private void detectEdges() {
        for (int i = 0; i < map.GetLength(0); i++) {
            for (int j = 0; j < map.GetLength(1); j++) {
                if (isInMapRange(i, j) && map[i, j] == Types.WALL) {
                    bool foundIt = false;
                    for (int k = i - 1; k <= i + 1; k++) {
                        for (int l = j - 1; l <= j + 1; l++) {
                            if (k == i || l == j) {
                                if (isInMapRange(k, l) && (map[k, l] == Types.GROUND)) {
                                    map[i, j] = Types.EDGE;
                                    foundIt = true;
                                    break;
                                }
                            }
                        }
                        if (foundIt) break;
                    }
                }
            }
        }
    }

    private void OnDrawGizmos() {
        if (map != null && enableDebug) {
            int realWidth = map.GetLength(0);
            int realHeight = map.GetLength(1);
            for (int i = 0; i < realWidth; i++) {
                for (int j = 0; j < realHeight; j++) {
                    Texture2D texture = null;
                    switch (map[i, j]) {
                        case Types.GROUND:
                            texture = groundTexture;
                            break;
                        case Types.WALL:
                            texture = wallTexture;
                            break;
                    }
                    Vector2 position = new Vector2(-realWidth / 2 + i, -realHeight / 2 + j);
                    Gizmos.DrawGUITexture(new Rect(position, Vector2.one), texture);
                }
            }
        }
    }
}
