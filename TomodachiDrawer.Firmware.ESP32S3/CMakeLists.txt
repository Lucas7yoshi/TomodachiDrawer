cmake_minimum_required(VERSION 3.16)

# Skip the esp_lcd components - we don't use them and GCC 15.2 (shipped with
# IDF v6.0.1's xtensa toolchain) ICEs while compiling esp_lcd_panel_rgb.c.
# Drop this once Espressif ships a working toolchain.
set(EXCLUDE_COMPONENTS esp_lcd)

include($ENV{IDF_PATH}/tools/cmake/project.cmake)

project(TomodachiDrawer_FW_ESP32S3)
