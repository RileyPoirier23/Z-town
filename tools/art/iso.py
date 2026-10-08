"""Shared projection for all generated art. Must match game/scripts/Iso.cs.

2:1 dimetric = orthographic camera, 45° azimuth, 30° elevation.
1 m along a floor axis = 64 px across / 32 px down; 1 m straight up = 78.4 px.
A storey (192 px) is therefore 2.45 m, and a 1.75 m adult is ~137 px tall.
Tile canvases are 128 x 256 with the floor diamond at the bottom:
  top (64,192) right (128,224) bottom (64,256) left (0,224).
"""
import math

import numpy as np

PX_PER_M = 90.51
ELEV = math.radians(30)
UP_PX = PX_PER_M * math.cos(ELEV)   # 78.4 px per vertical metre
TILE_W, TILE_H, CANVAS_W, CANVAS_H = 128, 64, 128, 256
STOREY_PX = 192
WALL_M = STOREY_PX / UP_PX          # 2.45 m

# canvas position of tile-space point (0,0,0)
ORIGIN = (64.0, 192.0)


def tile_to_canvas(u, v, h=0.0, origin=ORIGIN):
    """Tile space (u, v in tiles, h in metres) -> canvas pixels."""
    return (origin[0] + (u - v) * 64.0, origin[1] + (u + v) * 32.0 - h * UP_PX)


def view_vector():
    """Unit vector pointing from the scene toward the camera, in (x, y, z-up) world axes."""
    # the camera sits on the +x+y side, above (nearer things are lower on screen)
    d = np.array([1.0, 1.0, 0.0]) / math.sqrt(2)
    return np.array([d[0] * math.cos(ELEV), d[1] * math.cos(ELEV), math.sin(ELEV)])


# Light from the front-left of the screen: south-facing (+y) faces bright, east-facing (+x)
# faces darker, tops brightest. Matches the box shading in tiles.py.
LIGHT = np.array([0.25, 0.70, 0.67])
LIGHT = LIGHT / np.linalg.norm(LIGHT)
