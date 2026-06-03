cmake_minimum_required(VERSION 3.16)

# Skip components we don't use. ESP-IDF auto-includes everything in its tree;
# trimming the unused ones cuts the build size. Some entries below may end up
# being transitively required by other components and silently stay in the
# build - the ones that don't survive get dropped on the next round-trip.
#   esp_lcd       - we don't drive displays. Bonus: GCC 15.2 (IDF v6.0.1's
#                   xtensa toolchain) ICEs on esp_lcd_panel_rgb.c anyway.
#   Everything else here is network/wireless/storage/peripheral support that
#   has no role in a wired-USB-HID firmware.
set(EXCLUDE_COMPONENTS
    # Wireless / network stack
    esp_wifi
    bt
    esp_eth
    esp_coex
    esp_netif
    esp_netif_stack
    lwip
    wpa_supplicant
    esp_http_client
    esp_http_server
    esp_https_ota
    esp_https_server
    esp-tls
    mqtt
    mdns
    protocomm
    wifi_provisioning
    esp_local_ctrl
    esp_hid
    ieee802154
    openthread
    tcp_transport
    http_parser

    # Peripheral drivers we don't use. Keep: esp_driver_gpio, esp_driver_rmt,
    # esp_driver_uart (console).
    esp_lcd
    esp_adc
    esp_driver_ana_cmpr
    esp_driver_cam
    esp_driver_dac
    esp_driver_gptimer
    esp_driver_i2c
    esp_driver_i2s
    esp_driver_isp
    esp_driver_jpeg
    esp_driver_ledc
    esp_driver_mcpwm
    esp_driver_parlio
    esp_driver_pcnt
    esp_driver_ppa
    esp_driver_sdio
    esp_driver_sdm
    esp_driver_sdmmc
    esp_driver_sdspi
    esp_driver_spi
    esp_driver_touch_sens
    esp_driver_tsens
    esp_driver_usb_serial_jtag

    # Legacy meta-components that pull driver_* siblings in via REQUIRES.
    # Excluding these is what lets the esp_driver_* exclusions above bite.
    driver
    usb

    # Storage filesystems we don't use.
    fatfs
    spiffs
    wear_levelling

    # Misc utilities not used by our firmware.
    espcoredump
    esp_gdbstub
    esp_pm
    esp_psram
    json
    ulp
    cmock
    unity
    app_trace
    perfmon
    protobuf-c
    nvs_sec_provider
    touch_element
    idf_test
    rt
)

include($ENV{IDF_PATH}/tools/cmake/project.cmake)

project(TomodachiDrawer_FW_ESP32S3)
