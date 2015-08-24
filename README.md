# [dungeoneering](http://ec2-54-201-237-107.us-west-2.compute.amazonaws.com/dungeoneering/dungeoneering.html)
Voronoi polygon based dungeon layout editor. 
Runs from a desktop web browser which has the Unity plugin installed.

[dungeoneering](https://github.com/col42dev/dungeoneering) uses a [Delaunay Tessalation](https://github.com/jceipek/Unity-delaunay) implementation to represent dungeon layout editing. The editor canvas is tessellated on startup and tile placement will then use these tesslations. Tiles up to 4 layers high can be placed on the editor canvas, the starting world is already populated with tiles 1 layer high. Walls are generated for tiles based on the adjacent tile layer heights. 

dunegoeneering include a UI for placing ramps between adjacent tiles of differening heights. Geomertry for a stairway is dynamically mapped to the tiles. Doorways can be placed on tile edges, the geometry is dynamically mapped based on the edge transform.

An [A* navigation](http://arongranberg.com/astar/) simulation is running, a debug navigation agent (yellow sphere) will continually try to target the screen pointer world location. It is unable to traverse layers without using the ramp placements.

# screen shots

![Screen 1](https://raw.githubusercontent.com/col42dev/dungeoneering/master/Docs/Screen%20Shot%202015-08-24%20at%2015.10.20.png)

![Screen 2](https://raw.githubusercontent.com/col42dev/dungeoneering/master/Docs/Screen%20Shot%202015-08-24%20at%2015.10.38.png)
