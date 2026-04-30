# Generated Cmake Pico project file

cmake_minimum_required(VERSION 3.13)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# == DO NOT EDIT THE FOLLOWING LINES for the Raspberry Pi Pico VS Code Extension to work ==
if(WIN32)
    set(USERHOME $ENV{USERPROFILE})
else()
    set(USERHOME $ENV{HOME})
endif()
set(sdkVersion 2.2.0)
set(toolchainVersion 14_2_Rel1)
set(picotoolVersion 2.2.0-a4)
set(picoVscode ${USERHOME}/.pico-sdk/cmake/pico-vscode.cmake)
if (EXISTS ${picoVscode})
    include(${picoVscode})
endif()
# ====================================================================================
set(PICO_BOARD waveshare_rp2040_zero CACHE STRING "Board type")

# Pull in Raspberry Pi Pico SDK (must be before project)
include(pico_sdk_import.cmake)

project(TomodachiDrawer.Firmware C CXX ASM)

# Initialise the Raspberry Pi Pico SDK
pico_sdk_init()

add_executable(TomodachiDrawer.Firmware TomodachiDrawer.Firmware.c usb_descriptors.c)

pico_generate_pio_header(TomodachiDrawer.Firmware ${CMAKE_CURRENT_LIST_DIR}/ws2812.pio)

pico_set_program_name(TomodachiDrawer.Firmware "TomodachiDrawer.Firmware")
pico_set_program_version(TomodachiDrawer.Firmware "0.1")

# Disable stdio over UART and USB (we use TinyUSB directly)
pico_enable_stdio_uart(TomodachiDrawer.Firmware 0)
pico_enable_stdio_usb(TomodachiDrawer.Firmware 0)

target_link_libraries(TomodachiDrawer.Firmware
        pico_stdlib
        tinyusb_device
        tinyusb_board
        hardware_pio
    )

target_include_directories(TomodachiDrawer.Firmware PRIVATE
        ${CMAKE_CURRENT_LIST_DIR}
)

pico_add_extra_outputs(TomodachiDrawer.Firmware)

pico_set_binary_type(TomodachiDrawer.Firmware copy_to_ram)