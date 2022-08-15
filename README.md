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
|26 | 8 | start X  | int64 |
|35 | 8 | start Y  | int64 |
|44 | 8 | end X  | int64 |
|53 | 8 | end Y  | int64 |
|62 | 8 | delta X  | int64 |
|71 | 8 | delta Y  | int64 |
|80 | 8 | delta XX  | int64 |
|89 | 8 | delta YY  | int64 |
|98 | 8 | delta XY | int64 |
|107 | 8 | error  | int64 |
|116 | 8 | steps  | int64 |
|124 | 4 | checksum | int32 |

