using System.Collections.Generic;

public class Map {
    public int[,] mapData;
    public List<Room> rooms;

    public Map(int[,] mapData , List<Room> rooms) {
        this.mapData = mapData;
        this.rooms = rooms;
    }
}
