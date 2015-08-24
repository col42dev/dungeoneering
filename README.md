# [dungeoneering](http://ec2-54-201-237-107.us-west-2.compute.amazonaws.com/dungeoneering/dungeoneering.html)
Voronoi polygon based dungeon layout editor. 
Run from a desktop web browser which has the Unity plugin installed.

dungeoneering uses a [Delaunay Tessalation](https://github.com/jceipek/Unity-delaunay) implementation to represent dungeon layout editing. Tiles up to 4 layers high can be placed on the editor canvas. Walls are generated for tiles based on the adjacent tile layer heights. 

dunegoeneering include a UI for placing ramps between adjacent tiles of differening heights. Geomertry for a stairway is dynamically mapped to the tiles. Doorways can be placed on tile edges, the geometry is dynamically mapped based on the edge transform.

An [A* navigation](http://arongranberg.com/astar/) simulation is running, a debug navigation agent will continually try to target the screen pointer world location. 

# screen shots

![Screen 1](https://raw.githubusercontent.com/col42dev/dungeoneering/master/Docs/Screen%20Shot%202015-08-24%20at%2015.10.20.png)

![Screen 2](https://raw.githubusercontent.com/col42dev/dungeoneering/master/Docs/Screen%20Shot%202015-08-24%20at%2015.10.38.png)
