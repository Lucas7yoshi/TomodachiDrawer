idf_component_register(
    SRCS "TomodachiDrawer.Firmware.c" "usb_descriptors.c"
    INCLUDE_DIRS "."
    REQUIRES esp_partition esp_timer esp_driver_gpio
)
