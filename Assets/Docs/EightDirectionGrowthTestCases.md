# Test cases: phồng và đẩy theo 8 hướng

Tài liệu này là đặc tả kết quả mong đợi cho thuật toán phồng của Pet/Oven. Mỗi test phải được chạy cho cả Pet và Oven, trừ khi test ghi rõ loại đối tượng.

## 1. Quy ước bắt buộc

### Ký hiệu

| Ký hiệu | Ý nghĩa |
|---|---|
| `P` | Pet/Oven đang được cho ăn |
| `p` | Ô khác đã thuộc cùng body với `P` |
| `B`, `B1`, `B2` | Box có thể bị đẩy |
| `Q`, `q` | Body khác có thể bị đẩy nguyên khối |
| `@` | Player |
| `#` | Tường hoặc border cứng |
| `.` | Ô sàn trống |
| `+` | Ô mới được body chiếm sau khi phồng |
| `x` | Ô đã xét nhưng không được phồng vào |

Các sơ đồ được nhìn từ trên xuống. Hàng trên có `Y` lớn hơn; cột bên phải có `X` lớn hơn.

### Tám hướng

```text
NW (-1,+1)   N (0,+1)   NE (+1,+1)
 W (-1, 0)      P         E (+1, 0)
SW (-1,-1)   S (0,-1)   SE (+1,-1)
```

### Luật expected result

1. Mỗi lần ăn tạo tối đa một lớp gồm các ô lân cận 8 hướng của body hiện tại.
2. Vật nằm trong ô sắp phồng bị đẩy theo hướng áp lực. Nếu lực là chéo và không thực hiện được, box/body thử thành phần ngang trước rồi thành phần dọc. Player vẫn dùng luật tìm hướng thoát riêng.
3. Vật chỉ bị đẩy một ô trong một lần ăn.
4. Cho phép đẩy dây chuyền; vật xa body nhất phải di chuyển trước.
5. Mọi chuyển động phải được lập kế hoạch và kiểm tra trước khi thay đổi world.
6. Một nhánh gặp tường thì nhánh đó thất bại; các nhánh độc lập khác vẫn phồng (partial growth).
7. Chuyển động chéo từ `A` tới `D` chỉ hợp lệ nếu `D` và cả hai ô cạnh của đường chéo đều hợp lệ sau khi áp dụng kế hoạch.
8. Body nhiều ô luôn dịch chuyển nguyên khối, không được tách hoặc biến dạng khi bị đẩy.
9. Hai vật không được kết thúc trong cùng một ô.
10. Một vật không được nhận hai lệnh di chuyển trong cùng transaction.
11. Nếu transaction thất bại, không được để lại bất kỳ chuyển động hoặc blocker tạm nào.
12. Box và body khác chỉ fallback khi lực ban đầu là chéo, theo đúng thứ tự `chéo -> ngang -> dọc`. Player được fallback sang một trong 8 hướng và vẫn phải tuân theo luật không xuyên góc, không được đi vào ô body đang hoặc sắp chiếm.

### Luật chọn hướng thoát riêng cho player

Player thử hướng áp lực trước. Nếu hướng đó không hợp lệ, các hướng còn lại được xếp theo:

1. Tích vô hướng với hướng áp lực lớn hơn được ưu tiên trước, để player trượt gần hướng bị đẩy nhất.
2. Nếu bằng điểm, dùng thứ tự cố định `N, NE, E, SE, S, SW, W, NW` để kết quả không phụ thuộc thứ tự collection.
3. Một hướng chỉ hợp lệ khi ô đích trống, không thuộc body hiện tại, không thuộc growth layer sắp thêm, không bị entity khác đặt trước và không xuyên góc.
4. Nếu không có hướng hợp lệ, growth cell chứa player bị loại; player đứng yên và các nhánh độc lập vẫn phồng.

Ví dụ lực `E`:

```text
Thử E trước.
Nếu E bị chặn, NE và SE có cùng thành phần lực hướng Đông nên được xét tiếp.
Nếu hai hướng chéo cũng không hợp lệ, N được ưu tiên trước S theo tie-break cố định.
```

Quy tắc kiểm tra đường chéo:

```text
S D     A = vị trí hiện tại
A S     D = đích chéo
        S = hai ô cạnh phải không bị wall/vật đứng yên chặn
```

Ví dụ đi Đông Bắc từ `(x,y)`:

```text
destination = (x+1, y+1)
sideX       = (x+1, y)
sideY       = (x,   y+1)
```

## 2. Nhóm A — Phồng cơ bản

### TC-A01 — Body 1x1 phồng trong phòng trống

**Trước**

```text
y= 1   . . .
y= 0   . P .
y=-1   . . .
        -1 0 1   (x)
```

**Sau**

```text
y= 1   + + +
y= 0   + P +
y=-1   + + +
```

**Pass khi**

- Body có 9 ô: ô cũ và 8 ô mới.
- `growthLayers` ghi đúng 8 ô mới, không chứa ô gốc.
- Không có entity nào bị di chuyển.

### TC-A02 — Lần ăn thứ hai tạo thêm đúng một vòng

**Trước**

```text
. . . . .
. p p p .
. p P p .
. p p p .
. . . . .
```

**Sau**

```text
+ + + + +
+ p p p +
+ p P p +
+ p p p +
+ + + + +
```

**Pass khi**

- Body từ `3x3` thành `5x5`.
- Chỉ 16 ô vòng ngoài được lưu vào growth layer mới.
- Không thêm trùng những ô đã thuộc body.

### TC-A03 — Tường phía Bắc chặn cả ba nhánh phía Bắc

Áp dụng luật không đi xuyên góc: `NW` và `NE` cũng bị chặn vì đường chéo đi sát qua tường ở phía Bắc.

**Trước**

```text
. # .
. P .
. . .
```

**Sau**

```text
. # .
+ P +
+ + +
```

**Pass khi**

- Không thêm ô `N`, `NW`, `NE`.
- Vẫn thêm `W`, `E`, `SW`, `S`, `SE`.
- Lần ăn không bị hủy toàn bộ.

### TC-A04 — Body ở góc bản đồ

`#` đại diện cho ngoài map/border không thể đẩy.

**Trước**

```text
# # #
# P .
# . .
```

**Sau**

```text
# # #
# P +
# + +
```

**Pass khi**

- Không tạo cell hoặc blocker ngoài map.
- Chỉ thêm `E`, `S`, `SE`.

## 3. Nhóm B — Đẩy một vật

### TC-B01 — Đẩy box theo hướng Bắc

**Trước**

```text
y=2   . . .
y=1   . B .
y=0   . P .
y=-1  . . .
```

**Sau**

```text
y=2   . B .
y=1   + + +
y=0   + P +
y=-1  + + +
```

**Di chuyển mong đợi**

```text
B: (0,1) -> (0,2)
P thêm ô: (0,1)
```

Box phải di chuyển trước khi body chiếm vị trí cũ của box.

### TC-B02 — Đẩy box theo hướng Đông Bắc

**Trước**

```text
y=2   . . . .
y=1   . . B .
y=0   . P . .
y=-1  . . . .
        -1 0 1 2
```

**Di chuyển mong đợi**

```text
B:       (1,1) -> (2,2)
sideX:   (2,1) phải trống
sideY:   (1,2) phải trống
P thêm:  (1,1)
```

**Sau**

```text
y=2   . . . B
y=1   + + + .
y=0   + P + .
y=-1  + + + .
```

**Pass khi** box đi đúng `(1,1)`; vì hướng chéo hợp lệ nên không sử dụng fallback ngang hoặc dọc.

### TC-B03 — Chéo thất bại, ngang thất bại, fallback dọc

Box muốn đi Đông Bắc nhưng `sideX=(2,1)` là tường. Hướng ngang Đông cũng đi vào chính tường đó, vì vậy thuật toán phải thử hướng dọc Bắc.

**Trước**

```text
y=2   . . . .
y=1   . . B #
y=0   . P . .
y=-1  . . . .
```

**Thứ tự thử**

```text
NE -> thất bại vì không đủ clearance chéo
E  -> thất bại vì destination là #
N  -> thành công
```

**Sau**

```text
y=2   . . B .
y=1   + + + #
y=0   + P + .
y=-1  + + + .
```

**Pass khi**

- Box đi từ `(1,1)` tới `(1,2)`.
- Ô cũ của box `(1,1)` được thêm vào body.
- Nhánh `N`, `W`, `E`, `SW`, `S`, `SE` không liên quan vẫn có thể phồng.
- Box không xuyên góc tới `(2,2)` và không đi ngang vào tường `(2,1)`.

### TC-B04 — Đích chéo trống nhưng cả hai cạnh là tường

**Trước**

```text
. . . .
. # . .
. B # .
. P . .
```

Box ở `NE` của `P` muốn đi tiếp `NE`; ô đích trống nhưng hai ô cạnh của đường đi là `#`.

**Sau:** box giữ nguyên vì `NE`, `E` và `N` đều bị chặn; growth cell chứa box bị loại. Không có entity nào xuyên qua góc.

### TC-B05 — Box bị kẹt bởi tường phía sau

**Trước**

```text
. # .
. B .
. P .
. . .
```

**Sau**

```text
. # .
. B .
+ P +
+ + +
```

**Pass khi**

- Box giữ nguyên vì đích Bắc là tường.
- Ô `N` không phồng.
- Vì box không rời ô `N`, hai đường chéo `NW` và `NE` cũng bị chặn theo luật góc nghiêm ngặt.
- Năm hướng còn lại vẫn phồng.

## 4. Nhóm C — Đẩy dây chuyền

### TC-C01 — Hai box nối tiếp theo hướng Bắc

**Trước**

```text
. .  .
. B2 .
. B1 .
. P  .
```

**Sau**

```text
. B2 .   <- B2: y=2 -> y=3
. B1 .   <- B1: y=1 -> y=2
. +  .   <- P chiếm y=1
. P  .
```

**Thứ tự commit**

```text
1. B2 đi Bắc
2. B1 đi Bắc
3. Body thêm ô của B1
```

### TC-C02 — Hai box nối tiếp theo hướng Đông Bắc

**Trước theo tọa độ**

```text
P  = (0,0)
B1 = (1,1)
B2 = (2,2)
```

**Sau theo tọa độ**

```text
B2 = (3,3)
B1 = (2,2)
P thêm (1,1)
```

**Minh họa**

```text
Trước                  Sau
. . . . .              . . . . B2
. . . B2 .             . . . B1 .
. . B1 . .      ->     . . + . .
. P . . .              . P . . .
```

Phải kiểm tra đường chéo và hai ô cạnh cho cả `B1` lẫn `B2`.

### TC-C03 — Cuối dây chuyền gặp tường

**Trước**

```text
. #  .
. B2 .
. B1 .
. P  .
```

**Sau**

```text
. #  .
. B2 .
. B1 .
+ P  +
+ +  +
```

**Pass khi**

- `B1` và `B2` đều giữ nguyên.
- Không có trạng thái `B1` đã đi nhưng `B2` chưa đi.
- Các nhánh Bắc liên quan bị loại; nhánh độc lập khác vẫn phồng.

### TC-C04 — Ba box, box cuối có lối thoát

```text
Trước                     Sau
.  .  .  .                .  B3 .  .
.  B3 .  .                .  B2 .  .
.  B2 .  .       ->       .  B1 .  .
.  B1 .  .                .  +  .  .
.  P  .  .                .  P  .  .
```

**Pass khi** mỗi box chỉ di chuyển đúng một ô và được commit theo thứ tự `B3, B2, B1`.

## 5. Nhóm D — Player và body khác

### TC-D01 — Đẩy player theo hướng Bắc

**Trước**

```text
. . .
. @ .
. P .
```

**Sau**

```text
. @ .
. + .
. P .
```

Các hướng growth khác vẫn được thêm nếu trống. Player chỉ đi một ô và body chiếm ô cũ của player.

### TC-D02 — Player không có ô thoát

**Trước**

```text
. # .
. @ .
. P .
```

**Sau**

```text
. # .
. @ .
+ P +
+ + +
```

Player giữ nguyên; body không được đè lên player. Các nhánh không liên quan vẫn phồng.

### TC-D03 — Không dùng player để đẩy box tiếp

Luật đề xuất: player bị áp lực phồng không truyền lực sang box.

**Trước**

```text
. B .
. @ .
. P .
```

**Sau:** `P`, `@`, `B` đều giữ nguyên trên trục Bắc; các hướng độc lập khác vẫn phồng.

Test này ngăn chuỗi khó hiểu `P -> player -> box`.

### TC-D04 — Đẩy body 2x1 nguyên khối

**Trước**

```text
. . . . .
. Q q . .
. P . . .
```

`P` tạo áp lực Đông Bắc lên `Q`.

**Sau mong đợi**

```text
Q:  (x,y)   -> (x+1,y+1)
q:  (x+1,y) -> (x+2,y+1)
```

**Pass khi**

- Hai ô `Q/q` cùng dịch một vector.
- Khoảng cách và hình dạng body không đổi.
- Body đang phồng chiếm ô va chạm cũ.

### TC-D05 — Một ô của body 2x1 gặp tường

**Trước**

```text
. . # . .
. Q q . .
. P . . .
```

Nếu dịch chuyển khiến bất kỳ ô nào của `Q` hoặc đường đi chéo của nó gặp `#`, toàn bộ `Q` phải đứng yên và nhánh growth va chạm với `Q` bị loại.

Không được chỉ di chuyển một phần của `Q`.

### TC-D06 — Player trượt lên khi lực Đông bị tường chặn

Đây là trường hợp trong scene 5. Player ở bên phải body nên nhận lực `E`, nhưng ô bên phải là tường. Hướng `NE` không hợp lệ vì sẽ xuyên qua góc tường; `SE` cũng bị chặn. Ô phía Bắc trống nên player phải trượt lên.

**Trước**

```text
y=2   . . .
y=1   p @ #
y=0   . . #
```

**Thứ tự xét**

```text
E  -> fail: destination là wall
NE -> fail: đường chéo chạm side wall
SE -> fail: destination/side bị chặn
N  -> pass
```

**Sau**

```text
y=2   . @ .
y=1   p + #
y=0   . . #
```

**Pass khi**

- Player đi từ `(2,1)` lên `(2,2)`.
- Body chiếm ô cũ `(2,1)` của player.
- Player chỉ di chuyển một ô.
- Không thử `S` sau khi `N` đã hợp lệ.
- Food chỉ bị tiêu thụ sau khi toàn bộ plan commit thành công.

## 6. Nhóm E — Xung đột giữa nhiều lực

### TC-E01 — Hai proposal cùng tác động một box, cùng hướng

Hai cell nguồn cùng muốn đẩy `B` về Bắc.

**Trước — hai request cùng trỏ tới một box**

```text
Branch A --N--\
                > B tại (1,1)
Branch C --N--/
```

**Sau**

```text
y=2   . B .    B chỉ đi từ (1,1) tới (1,2)
y=1   . + .    body chiếm ô cũ của B
```

**Expected:** gộp thành đúng một `PushMove(B, N)`. Box chỉ đi một ô, không đi hai ô.

### TC-E02 — Hai lực chéo đối xứng tạo thành lực Bắc

```text
Lực 1: NE = (+1,+1)
Lực 2: NW = (-1,+1)
Tổng:       ( 0,+2)
Sign:       ( 0,+1) = N
```

**Trước**

```text
        B
       / \
     NE   NW
    p       p
```

**Sau**

```text
        B       <- đi lên đúng một ô
        ↑
        +       <- ô cũ của B được body chiếm
    p       p
```

**Expected:** box đi đúng một ô về Bắc.

### TC-E03 — Hai lực đối nghịch triệt tiêu

```text
Lực 1: E  = (+1,0)
Lực 2: W  = (-1,0)
Tổng:       ( 0,0)
```

**Trước**

```text
p  --E-->  B  <--W--  p
```

**Sau**

```text
p          B          p
           x
```

`B` giữ nguyên; `x` biểu thị growth proposal muốn chiếm ô của `B` đã bị loại.

**Expected:** box đứng yên và mọi growth proposal cần chiếm ô của box đều bị loại.

Không chọn hướng dựa trên thứ tự duyệt `HashSet`.

### TC-E04 — Hai vật muốn kết thúc trong cùng một ô

```text
B1 muốn đi SE -> T
B2 muốn đi SW -> T
```

**Trước**

```text
B1 . B2
.  T  .
.  P  .
```

**Sau**

```text
B1 . B2
.  T  .    T vẫn trống
.  P  .
```

**Expected:** cả hai push request xung đột đều bị hủy; `B1` và `B2` giữ nguyên. Không entity nào được chiếm `T`.

### TC-E05 — Chu kỳ đẩy

```text
B1 muốn vào ô B2
B2 muốn vào ô B1
```

**Trước**

```text
B1 <-> B2
```

**Trạng thái không được phép**

```text
B2     B1    <- đổi chỗ dù không có ô trống cuối chuỗi
```

**Sau hợp lệ**

```text
B1     B2    <- cả hai giữ nguyên
```

**Expected:** phát hiện cycle trong lúc lập kế hoạch; hủy cả hai lệnh. Không cho phép hai vật đổi chỗ vì chuỗi không kết thúc bằng ô trống.

## 7. Nhóm F — Tính toàn vẹn transaction

### TC-F01 — Rollback khi commit box thứ hai thất bại

Thiết lập giả lập để kế hoạch ban đầu hợp lệ, nhưng `B2.TryMove()` trả `false` lúc commit.

**Trước transaction**

```text
P B1 . B2 .

Position: P=(0,0), B1=(1,0), B2=(3,0)
Food: 1
```

**Trạng thái tạm khi commit lỗi**

```text
P . B1 B2 .
      ^
      B1 đã đi, move tiếp theo thất bại
```

**Sau rollback**

```text
P B1 . B2 .

Position, blocker và Food giống hệt trước transaction.
```

**Expected:** 

- Mọi box đã di chuyển trước đó trở về đúng vị trí cũ.
- Body chưa thêm bất kỳ growth cell nào.
- Player và body khác giữ nguyên.
- Dynamic blocker khớp hoàn toàn với trạng thái trước khi ăn.
- Food chưa bị tiêu thụ.

### TC-F02 — Chỉ tiêu thụ food sau khi commit thành công

**Growth thành công:** giảm food đúng 1.

```text
Trước                 Sau commit thành công
Food = 1              Food = 0
. P .         ->      + P +
```

**Không có bất kỳ growth cell hợp lệ:** không giảm food, trừ khi thiết kế chủ ý coi đây là hành động burp. Nếu vẫn dùng luật burp hiện tại, cần tách thành một testcase gameplay riêng và không để hành vi phụ thuộc lỗi push.

```text
Trước                 Sau plan thất bại
Food = 1              Food = 1
# P #         ->      # P #
```

### TC-F03 — Không để blocker bị mất sau exception

Buộc transaction ném exception sau khi đã bắt đầu commit move.

**Trước**

```text
P B .

CanEnter(P) = false
CanEnter(B) = false
```

**Trong lúc lỗi — trạng thái không được để lại**

```text
P . B

blocker(P) hoặc blocker(B) bị thiếu
```

**Sau rollback/finally**

```text
P B .

CanEnter(P) = false
CanEnter(B) = false
CanEnter(.) = true
```

**Expected:** rollback phục hồi blocker của Pet, Oven, box và body khác; `GridWorld.CanEnter()` tiếp tục trả kết quả đúng.

### TC-F04 — Burp chỉ xóa đúng growth layer cuối

1. Body 1x1 ăn lần một thành `3x3`.
2. Ăn lần hai thành `5x5`.
3. Burp một lần.

**Minh họa các trạng thái**

```text
Ban đầu       Ăn lần 1       Ăn lần 2       Burp một lần

  P           + + +         * * * * *       + + +
       ->      + P +    ->   * + + + *  ->   + P +
              + + +         * + P + *       + + +
                            * + + + *
                            * * * * *

+ = growth layer 1
* = growth layer 2
```

**Expected:** body trở về đúng `3x3`; 16 ô của lớp thứ hai bị xóa, 8 ô lớp thứ nhất còn nguyên. Box/player từng bị đẩy không bị tự động kéo ngược về.

## 8. Nhóm G — Regression cho code hiện tại

### TC-G01 — Pressure resolver phải trả hướng chéo

**Trước**

```text
y=1   . T
y=0   P .
```

```text
Source body cell: (0,0)
Growth target:    (1,1)
Expected force:   (1,1) = NE
```

**Sau khi resolve lực**

```text
y=1   . T   ↗ NE
y=0   P .
```

Nếu nhiều source cell cùng tác động target, cộng các vector áp lực rồi lấy `Sign(x, y)`. Chỉ vector tổng `(0,0)` mới bị coi là triệt tiêu.

### TC-G02 — Fallback chéo, ngang, dọc cho box và body khác

Khi lực là `NE`, box và body khác phải thử đúng thứ tự:

```text
1. NE
2. E
3. N
```

Không thử `W`, `S`, `NW`, `SE` hoặc `SW`.

**Trước**

```text
. . .
. B #    B nhận lực NE; NE và E bị chặn bởi # phía Đông
. P .
```

**Sau**

```text
. B .    B fallback lên Bắc
. + #    ô cũ của B được growth chiếm
. P .
```

Nếu cả ba hướng đều thất bại, box/body đứng yên và ô growth liên quan bị loại. Player tiếp tục tuân theo luật chọn hướng thoát riêng ở phần đầu tài liệu cùng `TC-D06`.

### TC-G03 — Hướng chéo không xuyên góc trước khi fallback

```text
Box:         (1,1)
Direction:   NE
Destination: (2,2) trống
SideX:       (2,1) là wall
SideY:       (1,2) trống
Expected cho lần thử NE: false
```

**Trước**

```text
y=2   . . D
y=1   . B #
y=0   . P .
```

Kết quả `false` của lần thử NE không hủy toàn bộ nhánh. Planner tiếp tục thử `E`, rồi `N`.

**Sau khi áp dụng fallback**

```text
y=2   . B D    D vẫn trống; B đi Bắc
y=1   . + #    ô cũ của B được growth chiếm
y=0   . P .
```

### TC-G04 — Kết quả không phụ thuộc thứ tự `FindObjectsByType`

Chạy cùng một level nhiều lần và đảo thứ tự đăng ký `B1`, `B2`, `Q`, player trong occupancy map.

**Hai input có cùng bản đồ nhưng khác thứ tự đăng ký**

```text
Run A registration: B1, B2, Q, Player
Run B registration: Player, Q, B2, B1

Map của cả hai run:
. B2 .
. B1 .
. P  .
```

**Sau — cả hai run phải giống nhau**

```text
Run A              Run B
. B2 .             . B2 .
. B1 .             . B1 .
. +  .             . +  .
. P  .             . P  .
```

**Expected:** growth cells, push directions và vị trí cuối luôn giống nhau.

## 9. Checklist assertion dùng cho mọi testcase

Sau mỗi lần ăn, kiểm tra:

- Không có hai entity khác nhau chiếm cùng grid cell.
- Không entity nào nằm trong wall, border cứng hoặc ngoài map.
- Mọi `PetBody.bodyCells` là duy nhất.
- Mọi body nhiều ô vẫn có hình dạng hợp lệ và không bị tách do push.
- `growthLayers[^1]` chỉ chứa các ô thực sự được thêm trong lần ăn đó.
- Mỗi box/player/body bị đẩy nhiều nhất một grid step.
- Chuyển động chéo không xuyên qua ô cạnh bị chặn.
- Dynamic blocker khớp với vị trí cuối của mọi entity.
- Không còn blocker tạm bị suspend sau khi resolver kết thúc.
- Kết quả không thay đổi giữa các lần chạy với cùng input.
- Food chỉ bị trừ theo đúng luật sau khi transaction thành công.

## 10. Thứ tự triển khai và chạy test

1. Chạy `TC-G01` để sửa hướng áp lực chéo.
2. Chạy nhóm A để xác nhận sinh growth layer 8 hướng.
3. Chạy nhóm B để thêm kiểm tra đường chéo cho một vật.
4. Chạy nhóm C để triển khai dependency chain.
5. Chạy nhóm D cho player, player fallback và body nhiều ô.
6. Chạy nhóm E để xử lý xung đột và kết quả deterministic.
7. Chạy nhóm F để bảo đảm transaction/rollback.
8. Cuối cùng chạy toàn bộ nhóm G để tránh regression.
