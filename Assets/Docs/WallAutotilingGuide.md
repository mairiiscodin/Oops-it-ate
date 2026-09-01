# Hướng dẫn hiển thị tường và border bằng 2 asset South

Tài liệu này mô tả cách chọn một trong hai hình cho tường `#` và border ngoài map. Mỗi loại chỉ phân biệt cạnh phía South đang hở hay đang nối tiếp với ô cùng loại.

> Thuật toán đẩy và di chuyển vẫn hỗ trợ 8 hướng. Việc chọn sprite tường trong phiên bản này chỉ kiểm tra một ô ở hướng South.

## 1. Hai field trong GridTileTheme

```text
wallSouthOpen    = sprite hở hướng South
wallSouthClosed  = sprite không hở hướng South

borderSouthOpen   = sprite border hở hướng South
borderSouthClosed = sprite border không hở hướng South
```

Kéo hai sprite vào asset `Assets/Assets/Grid Tile Theme.asset` trong Inspector.

Quy tắc chọn:

```text
                 ô tường hiện tại
                         #
                         |
                kiểm tra ô South
                   /             \
           không phải #         là #
                |                 |
       wallSouthOpen      wallSouthClosed
```

Nếu một field cùng loại chưa được gán, code tạm dùng sprite còn lại của loại đó để ô không bị vô hình. Cần gán đủ bốn field để thấy đúng hình.

## 2. Lỗi trước đây

Trong Scene 5, dữ liệu tường là:

```text
K#..P
.#.#.
.#.#.
S#..P
...#.
...##
```

Trước đây mọi ô `#` đều lấy cùng một sprite `theme.wall`, nên không thể phân biệt đáy tường hở với đoạn tường tiếp tục đi xuống:

```text
Sai về hình ảnh

  [===]
  [===]   <- ba ô cùng một hình nằm ngang
  [===]
```

Va chạm của ô vẫn có thể đúng, nhưng hình tường không phản ánh các ô hàng xóm. Cần tách hai khái niệm:

- **Dữ liệu/va chạm:** ô nào là tường và không thể đi xuyên qua.
- **Hiển thị:** ô tường dùng sprite đầu, đoạn thẳng, góc, chữ T hay giao bốn hướng.

## 3. Thuật toán 2 trạng thái

```csharp
bool southOpen = !isWall(position + South);
Sprite sprite = theme.GetWallSprite(southOpen);
```

| Ô South | Trạng thái | Sprite |
|---|---|---|
| Không phải tường `#` | Hở South | `wallSouthOpen` |
| Là tường `#` | Không hở South | `wallSouthClosed` |

## 4. Minh họa Scene 5

Ký hiệu minh họa:

- `▽`: dùng `wallSouthOpen`.
- `■`: dùng `wallSouthClosed`.

```text
Dữ liệu               Sprite được chọn

K # . . P             K ■ . . P
. # . # .             . ■ . ▽ .
. # . # .             . ■ . ▽ .
S # . . P             S ▽ . . P
. . . # .             . . . ■ .
. . . # #             . . . ▽ ▽
```

Giải thích:

- Ba ô trên của cột tường bên trái đều còn tường ngay phía South nên dùng `wallSouthClosed`.
- Ô cuối cột bên trái không có tường phía South nên dùng `wallSouthOpen`.
- Hai tường nằm cạnh nhau theo chiều ngang ở hàng cuối đều hở South; cả hai dùng `wallSouthOpen`.
- Tường bên Đông, Tây, Bắc hoặc chéo không ảnh hưởng lựa chọn sprite.

## 5. Testcase trước/sau

### 5.1. Một tường đứng riêng

```text
Trước       Sau

  #           ▽
```

Kết quả: phía South trống, dùng `wallSouthOpen`.

### 5.2. Cột tường

```text
Trước       Sau

  #           ■
  #           ■
  #           ▽
```

Hai ô trên dùng `wallSouthClosed`; ô dưới cùng dùng `wallSouthOpen`.

### 5.3. Hàng tường ngang

```text
Trước       Sau

  # # #       ▽ ▽ ▽
```

Cả ba ô đều không có tường phía South nên đều dùng `wallSouthOpen`.

### 5.4. Tường chạm chéo

```text
Trước       Sau

  # .         ▽ .
  . #         . ▽
```

Hai ô không nằm trực tiếp trên/dưới nhau nên đều hở South.

### 5.5. Tường ở trên một border

```text
  #        -> wallSouthOpen
  B        -> border dùng hệ thống sprite border riêng
```

Border `B` không phải tường `#`, vì vậy không đóng phía South của tường. Nếu sau này muốn border cũng đóng tường, phải thay điều kiện bằng kiểm tra `isWall || isBorder` một cách chủ động.

## 6. Border ngoài map

Border áp dụng cùng thuật toán nhưng chỉ kiểm tra border khác:

```csharp
bool southOpen = !isBorder(position + South);
Sprite sprite = theme.GetBorderSprite(southOpen);
```

```text
Border hiện tại
      B
      |
kiểm tra ô South
  /             \
không phải B     là B
     |             |
Border South   Border South
Open           Closed
```

Border chỉ xét border; tường `#` ngay phía South không làm border chuyển sang trạng thái đóng.

## 7. Checklist nghiệm thu

- [ ] Inspector có `Wall South Open` và `Wall South Closed`.
- [ ] Inspector có `Border South Open` và `Border South Closed`.
- [ ] Có thể kéo bốn sprite riêng vào bốn ô này.
- [ ] Tường có một tường ngay bên dưới dùng `Wall South Closed`.
- [ ] Tường không có tường ngay bên dưới dùng `Wall South Open`.
- [ ] Hàng tường ngang dùng `Wall South Open` cho từng ô nếu phía dưới trống.
- [ ] Tường chạm đường chéo không bị coi là đóng South.
- [ ] Border có border ngay bên dưới dùng `Border South Closed`.
- [ ] Border không có border ngay bên dưới dùng `Border South Open`.
- [ ] Border và tường không ảnh hưởng phép kiểm tra South của nhau.
