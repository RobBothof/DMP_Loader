# DMP_LOADER

The Draw Machine Project 'Loader' can load drawinstruction files and feed them line by line to the painter/plotter/motioncontroller via usb-serial port.

### .dri file format
| start | length | description | data/type |
|:---   |:---    | :--- | :--- |
|0      | 20     | header | "DRI::DrawInstruction" | 
|20 | 4 | .dri version | int32 |
|24 | 8 | number of instructions | int64 |
|32 | 8 | start byte index of payload | int64 |
|40 | 1 | size per instruction | uint8 |
| -----   | -----    | ----- | ----- |
|start + index*size | size | drawinstruction |  |
|... | ... | ... |  |



### DrawInstruction format (bytes)

The payload is interleaved with a 0x00 byte spacer.

| start | length | description | data/type |
|:---   |:---    | :--- | :--- |
|0      | 10     | header | 10 x 0xFF | 
|10 | 1 | size of payload | uint8 |
|11 | 8 | index  | int64 |
|20 | 1 | linetype  | uint8 |
|22 | 1 | x direction  | int8 |
|24 | 1 | y direction  | int8 |
|26 | 4 | start X  | int32 |
|31 | 4 | start Y  | int32 |
|36 | 4 | end X  | int32 |
|41 | 4 | end Y  | int32 |
|46 | 8 | delta X  | int64 |
|55 | 8 | delta Y  | int64 |
|64 | 8 | delta XX  | int64 |
|73 | 8 | delta YY  | int64 |
|82 | 8 | delta XY | int64 |
|91 | 8 | error  | int64 |
|100 | 8 | steps  | int64 |
|108 | 4 | checksum | int32 |

