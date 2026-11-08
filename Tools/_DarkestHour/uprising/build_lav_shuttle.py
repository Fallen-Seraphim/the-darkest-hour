#!/usr/bin/env python3
"""Мини-шаттл LA-V (решение автора 2026-11-07): на нём прилетает эвакуационный борт «Аэронавтики» вместе с отрядом
ЧВК LA-V — вместо большого вокзального шаттла (arrivals). 7×13 тайлов, нос на север: кабина с креслом пилота,
салон на 10 мест вдоль бортов, боковые шлюзы и кормовая рампа, два двигателя-декорации за кормой.
Пульта управления нет: борт стоит на ЗП-1 неподвижно (Static) и улетает по таймеру режима.

Выход: Resources/Maps/_DarkestHour/Uprising/lav_shuttle.yml (грид; грузит DHUprisingRuleSystem.Arrive).
Запуск из корня репозитория:  python3 Tools/_DarkestHour/uprising/build_lav_shuttle.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_map as bm  # noqa: E402

FLOOR = 'FloorShuttleBlue'
AISLE = 'FloorShuttleWhite'
WALL = 'WallShuttle'
WINDOW = 'ShuttleWindow'
DOOR = 'AirlockGlassShuttle'
LIGHT = 'AlwaysPoweredWallLight'


def build():
    lv = bm.Level(0)
    x0, y0, x1, y1 = -3, -6, 3, 6
    lv.box(x0, y0, x1, y1, WALL, FLOOR)
    lv.floor(-1, y0 + 1, 1, y1 - 1, AISLE)
    # остекление: нос и борта салона
    for x in (-1, 0, 1):
        lv.window(x, y1, WINDOW)
    for y in (3, 0, -3):
        lv.window(x0, y, WINDOW)
        lv.window(x1, y, WINDOW)
    # кабина: кресло пилота, по бокам аптечка и аварийный шкаф
    lv.put('ChairPilotSeat', 0, y1 - 1, bm.ROT_N)
    lv.put('TableReinforced', -2, y1 - 1)
    lv.put('MedkitFilled', -2, y1 - 1)
    lv.put('ClosetEmergencyFilledRandom', 2, y1 - 1)
    # салон: по 5 кресел у каждого борта, лицом к проходу
    for y in range(y1 - 3, y0 + 1, -1):
        if y == -1:
            continue          # место напротив шлюзов
        lv.put('Chair', -2, y, bm.ROT_E)
        lv.put('Chair', 2, y, bm.ROT_W)
    # шлюзы по бортам и кормовая рампа
    lv.door(x0, -1, DOOR)
    lv.door(x1, -1, DOOR)
    lv.door(0, y0, DOOR)
    # свет
    lv.put(LIGHT, 0, y1 - 2, bm.ROT_N)
    lv.put(LIGHT, 0, y0 + 1, bm.ROT_S)
    # двигатели за кормой (декор)
    for x in (-2, 2):
        lv.floor(x, y0 - 1, x, y0 - 1, 'Plating')
        lv.put('Thruster', x, y0 - 1, bm.ROT_S)
    return lv


def main():
    lv = build()
    path = bm.OUT + '/lav_shuttle.yml'
    protos, tiles = bm.write(lv, path, 'LA-V shuttle', False)
    print('OK' if bm.check(set(protos), tiles) else 'ERRORS')


if __name__ == '__main__':
    main()
