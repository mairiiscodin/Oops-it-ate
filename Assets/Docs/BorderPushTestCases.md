# Test cases: cho border (tường biên) ăn và ợ lại

Tài liệu này mô tả cơ chế cho border ăn (`GridWorld.TryPushBorder`) và ợ lại (`GridWorld.BurpVoidsOverTime`), kèm các test case mong đợi. Những chỗ thiết kế còn chưa chốt được đánh dấu **⚠ Cần quyết định**.

**Luật thiết kế gốc:** khi cho border ăn, toàn bộ dải tường thẳng trước mặt player (mọi ô border liền nhau có sàn ngay phía sau) lùi ra 1 ô.

File liên quan:

- `Assets/Scripts/Grid/GridCellMap.cs`: dữ liệu ô (sàn, tường, border, blocker).
- `Assets/Scripts/Grid/GridWorld.cs`: đẩy border, ợ, fit camera.
- `Assets/Scripts/Interaction/PlayerInteractor.cs`: thứ tự ưu tiên khi cho ăn.
- `Assets/Scripts/Levels/DoorExit.cs`: cửa đi theo border.

## 1. Cơ chế

### 1.1. Bốn loại ô

| Tập trong `GridCellMap` | Nguồn | Đi vào được? |
|---|---|---|
| `loadedCells` | `.`, `#`, `S`, `K`, `P`, `B` trong map | Có, nếu không bị chặn |
| `authoredWalls` | `#` | Không (là ô loaded nhưng bị chặn) |
| `borderCells` | `_`, chữ số `1`–`9` (cửa), và vòng border mặc định | Không (không phải ô loaded) |
| `dynamicBlockers` | Ô của Box, PetBody (Pet/Oven) | Không |

Khi khởi tạo, `BuildDefaultOuterBorder()` luôn thêm một vòng border quanh hình chữ nhật `[MinX-1 .. MaxX+1] × [MinY-1 .. MaxY+1]`. Vì vậy mọi map đều có biên ăn được, kể cả khi không vẽ `_`.

`MinX/MaxX/MinY/MaxY` là kích thước grid gốc và **không đổi** sau khi đẩy border. Dải tường không bị giới hạn bởi các giá trị này.

### 1.2. Thứ tự ưu tiên khi player bấm tương tác

Player đang cầm food và nhìn vào ô `T`. Code thử lần lượt:

1. Kitchen tại `T` (chỉ khi chưa cầm food) → lấy food.
2. Body (Pet/Oven) tại `T` → cho body ăn.
3. Cửa tại `T` (`TryFeedDoorAt`) → cửa phồng rộng.
4. Border tại `T` và `T` không phải cửa ẩn → `TryPushBorder(T, FacingDirection)`.
5. Không có gì phù hợp → âm `Nope`, food giữ nguyên.

Food chỉ bị trừ khi `TryPushBorder` trả `true`. Âm thanh khi thành công là `Eat`, sau đó `BorderPush`.

### 1.3. Thuật toán `TryPushBorder(pos, dir)`

`dir` là hướng player đang nhìn, tức hướng **ra ngoài**.

**Bước 0 — Điều kiện vào**

- `dir` phải là hướng chính (N/E/S/W).
- `pos` phải là border.

**Bước 1 — Tìm dải tường (`FindContiguousBorderLine`)**

- Một ô đẩy được (`IsPushableBorderCell`) khi nó là border và ô phía trong `ô - dir` là ô loaded.
- Code lấy `tangent` vuông góc với `dir`, rồi đi lần theo `+tangent` và `-tangent` từ `pos`.
- Mỗi ô vẫn đẩy được thì được gom vào `borderLine`.
- Gặp ô đầu tiên không đẩy được thì dừng ở phía đó. Không có giới hạn theo kích thước grid gốc, nên cạnh đã được nới bởi lần đẩy trước vẫn được đẩy đủ.

**Bước 2 — Bù góc (`FindAddedCornerCells`)**

- Ở mỗi đầu dải, code lấy ô ngoài cùng `extreme`, rồi xét `corner = extreme + tangent`.
- Nếu `corner` là border nhưng **không** đẩy được, ô `corner + dir` được thêm vào `addedCornerCells` để góc vẫn kín sau khi dải lùi ra.
- Nếu `corner + dir` đã là border sẵn (ví dụ vách trong chạm biên ngoài), góc đã kín nên không thêm gì.

**Bước 3 — Kiểm tra (`CanShiftBorderLine`)**

Mỗi ô đích (`ô + dir`) và mỗi ô góc bù **không** được là:

- tường `#`;
- border;
- vị trí player;
- ô thuộc một cửa không nằm trên dải đang đẩy.

**Bước 3b — Đẩy vật vướng (`TryPlanOccupantPushes`)**

- Box hoặc body (Pet/Oven) đứng ở ô đích hay ô góc bù sẽ bị đẩy ra 1 ô theo `dir`. Body bị đẩy nguyên khối.
- Nếu ô phía trước vật đó lại có box/body khác, vật đó cũng bị đẩy theo (dây chuyền).
- Mỗi ô mà vật sẽ đi tới phải là sàn, không phải `#`, border hay player, và không bị vật đứng yên chiếm.
- Body có `canBePushedByBodyGrowth = false` thì không đẩy được.

Chỉ cần một ô hoặc một vật vi phạm là cả lượt bị hủy. World không bị thay đổi gì và food không bị trừ.

**Bước 4 — Commit**

Các vật vướng được dịch trước. Sau đó, với mỗi ô `s` trong dải, `d = s + dir`:

- `s` bỏ khỏi border và trở thành ô loaded (sàn mới).
- Nếu `d` đang là ô loaded thì gỡ nó khỏi sàn và ghi vào `emptiedCells`.
- `d` trở thành border.

Sau đó:

- Ô góc bù trở thành border, xử lý `emptiedCells` giống như trên.
- Mọi cửa chạm dải (`TouchesAny`) được `Shift(dir)`.
- Một `VoidPushLayer(dir, borderLine, addedCornerCells, emptiedCells, movedDoors)` được push vào stack `voidPushLayers`.
- Camera fit lại (có pixel-snap) và timer ợ được **reset**.

### 1.4. Ợ lại (`BurpVoidsOverTime`)

- Stack được xử lý theo LIFO, mỗi lần ợ một layer.
- Code chờ **3 giây** rồi lấy layer trên cùng ra. Mỗi lần đẩy border mới đều reset bộ đếm.
- `CanRestoreVoid(layer)` chỉ trả true khi trên `FilledCells` không còn:
  - ô cửa;
  - player;
  - dynamic blocker;
  - tường `#`.
- Nếu chưa trả true, cứ mỗi **0.25 giây** code gọi `TryClearVoidCell` để đẩy mọi thứ trên `FilledCells` vào trong (`-dir`):
  - `GridMover` (player) đứng trong `FilledCells` thì bước vào trong nếu `CanMoveTo`;
  - box đứng trong `FilledCells` thì đẩy player chắn phía trong trước (nếu có), rồi `box.TryMove(-dir)`;
  - `PetBody` chạm `FilledCells` thì đẩy player trước nếu body sau khi dịch sẽ đè lên player, rồi `body.TryShift(-dir)`.
- `RestoreVoid(layer)` lần lượt:
  1. Dời các cửa đã đi theo về `-dir`.
  2. Biến `FilledCells` lại thành border và gỡ khỏi sàn.
  3. Bỏ border ở các ô `FilledCells + dir`.
  4. Bỏ border ở các ô góc bù.
  5. Trả các `emptiedCells` lại thành sàn.
  6. Fit lại camera và phát âm `Burp`.
- Ợ **không** hoàn lại food.

## 2. Quy ước test

| Ký hiệu | Ý nghĩa |
|---|---|
| `_` | Border (ăn được) |
| `#` | Tường cứng `#` |
| `.` | Sàn |
| `+` | Sàn mới sinh ra từ border cũ |
| `x` | Sàn bị border mới đè lên (`emptiedCells`) |
| `@` | Player; mũi tên `→ ← ↑ ↓` là hướng nhìn |
| `B` | Box |
| `P`, `p` | Pet/Oven body |
| `D` | Cửa |
| ` ` (trống) | Ô không loaded và không phải border (void) |

Hàng trên có `Y` lớn hơn và cột phải có `X` lớn hơn. Các ví dụ mặc định dùng phòng `3x3` với `MinX=MinY=0`, `MaxX=MaxY=2`, cùng vòng border mặc định ở `x=-1`, `x=3`, `y=-1` và `y=3`.

## 3. Nhóm A — Đẩy cơ bản

### TC-A01 — Đẩy cả cạnh Đông

**Trước** — player `(2,1)` nhìn Đông vào `(3,1)`.

```text
y= 3   _ _ _ _ _
y= 2   _ . . . _
y= 1   _ . . @→_
y= 0   _ . . . _
y=-1   _ _ _ _ _
       -1 0 1 2 3
```

**Sau**

```text
y= 3   _ _ _ _ _ _
y= 2   _ . . . + _
y= 1   _ . . @ + _
y= 0   _ . . . + _
y=-1   _ _ _ _ _ _
       -1 0 1 2 3 4
```

**Pass khi**

- `borderLine = {(3,0),(3,1),(3,2)}`: cả cạnh bị đẩy, không chỉ ô trước mặt.
- `addedCornerCells = {(4,3),(4,-1)}`.
- Hai góc cũ `(3,3)` và `(3,-1)` vẫn là border.
- Food giảm 1; âm `Eat` và `BorderPush` phát.
- Có đúng 1 layer trong `voidPushLayers`; camera fit lại để thấy cột `x=4`.

### TC-A02 — Đẩy cùng một cạnh hai lần

Tiếp từ TC-A01, player đi tới `(3,1)` rồi nhìn Đông vào `(4,1)`.

**Sau**

```text
y= 3   _ _ _ _ _ _ _
y= 2   _ . . . + + _
y= 1   _ . . . + @ _
y= 0   _ . . . + + _
y=-1   _ _ _ _ _ _ _
       -1 0 1 2 3 4 5
```

**Pass khi**

- `borderLine = {(4,0),(4,1),(4,2)}`.
- Góc bù `(5,3)` và `(5,-1)` được thêm.
- Stack có 2 layer, layer mới nằm trên cùng.

### TC-A03 — Bốn hướng đều đẩy được

Lặp TC-A01 cho từng hướng N/S/W/E trên phòng `3x3` mới.

**Pass khi** mỗi hướng đẩy đúng 3 ô cạnh tương ứng, đúng 2 ô góc bù, và không có ô nào của cạnh khác bị đổi.

### TC-A04 — Không cầm food

Player nhìn border nhưng không cầm food.

**Pass khi** `TryPushBorder` không được gọi, world không đổi và không phát âm thanh.

## 4. Nhóm B — Vật vướng phía sau tường và các trường hợp bị chặn

Luật: vật đứng ở chỗ tường sẽ lùi tới bị đẩy lùi theo 1 ô. Nếu vật không lùi được thì tường cũng không đẩy được.

Khi lượt đẩy bị hủy: world giữ nguyên (kể cả vật không bị dịch dở), food không bị trừ, phát âm `Nope`, có log warning `something blocks the row in front of it` và không thêm layer.

Thực tế ô ngoài biên phòng thường là void, nên các test có vật phía sau dùng một ô `_` đứng tự do trong phòng (xem TC-D02).

### TC-B01 — Box phía sau tường bị đẩy lùi

**Trước**

```text
y= 3   _ _ _ _ _ _ _
y= 2   _ . . . . . _
y= 1   _ . @→_ B . _
y= 0   _ . . . . . _
y=-1   _ _ _ _ _ _ _
       -1 0 1 2 3 4 5
```

`borderLine = {(2,1)}` và không có góc bù, vì `(2,2)` và `(2,0)` là sàn.

**Sau**

```text
y= 1   _ . @ + _ B _
```

**Pass khi**

- Box đi `(3,1) → (4,1)` đúng 1 ô.
- `(2,1)` thành sàn, `(3,1)` thành border.
- Dynamic blocker chỉ còn ở `(4,1)`.

### TC-B02 — Box không lùi được thì tường không đẩy được

Giống TC-B01 nhưng ô `(4,1)` là `#`, border hoặc void.

**Pass khi** cả lượt bị hủy, box đứng yên và food giữ nguyên.

### TC-B03 — Đẩy dây chuyền hai box

```text
y= 1   _ . @→_ B1 B2 . _
```

**Pass khi** `B2` đi 1 ô, rồi `B1` đi 1 ô, rồi tường lùi. Nếu ô phía trước `B2` bị chặn thì cả chuỗi và tường đều đứng yên.

### TC-B04 — Pet/Oven phía sau tường bị đẩy nguyên khối

Giống TC-B01 nhưng thay `B` bằng một body 2 ô `Q q` nằm ngang.

**Pass khi**

- Cả hai ô của body dịch đúng 1 ô và giữ nguyên hình dạng.
- Nếu bất kỳ ô nào của body không có chỗ đi, cả body và tường đều đứng yên.
- Body có `canBePushedByBodyGrowth = false` thì không bị đẩy và tường cũng không đẩy được.

### TC-B05 — Vật nằm ở ô góc bù

Một box đứng ở ô `corner + dir` (ô sẽ thành border để giữ góc kín).

**Pass khi** box cũng bị đẩy 1 ô theo `dir`, giống vật ở ô đích của dải.

### TC-B06 — Tường `#` ở ô đích

Giống TC-B01 nhưng thay `B` bằng `#`.

**Pass khi** đẩy thất bại.

### TC-B07 — Cửa khác ở ô đích

Ô đích chứa một cửa không nằm trên dải đang đẩy.

**Pass khi** đẩy thất bại (`HasUnmovedDoorAt`).

### TC-B08 — Ô góc bù bị chặn

Chỉ ô góc bù `corner + dir` bị chặn (ví dụ có `#`), các ô đích của dải đều trống.

**Pass khi** cả lượt bị hủy, không có trạng thái "dải đã đẩy nhưng góc thì không".

### TC-B09 — Ô trước mặt không phải border

Player nhìn vào sàn, `#` hoặc void.

**Pass khi** `TryPushBorder` trả false ngay ở Bước 0. Nếu nhìn vào ô loaded/`#` thì có warning `neither an '_' border nor a default outer border`. Food giữ nguyên và phát `Nope`.

## 5. Nhóm C — Hình dạng dải

### TC-C01 — Phòng hình chữ L: chỉ đẩy phần có sàn phía sau

`#` là ô loaded, nên nó **vẫn được tính** là "có sàn phía sau". Test này dùng void để cắt dải.

```text
y= 3   _ _ _
y= 2   _ . _ _ _
y= 1   _ . . . _
y= 0   _ . . @→_
y=-1   _ _ _ _ _
       -1 0 1 2 3
```

Phòng có hình chữ L: hàng `y=2` chỉ có sàn ở `x=0`, còn `(1,2)`, `(2,2)` và `(3,2)` là border. Player `(2,0)` nhìn Đông vào `(3,0)`.

**Pass khi**

- `borderLine = {(3,0),(3,1)}`; dải dừng ở `(3,2)` vì ô phía trong `(2,2)` là border chứ không phải sàn.
- Góc bù được thêm tại `(4,2)` (từ góc `(3,2)`) và `(4,-1)` (từ góc `(3,-1)`).
- Cạnh phía trên `(1,2)` và `(2,2)` không đổi.

### TC-C02 — Ô phía sau là `#` vẫn được đẩy (theo thiết kế)

```text
_ _ _ _ _
_ . . # _
_ . . @→_
_ _ _ _ _
```

**Pass khi** ô border cạnh `#` vẫn nằm trong dải và bị đẩy. Sàn mới ở cột đó là sàn thường, còn `#` giữ nguyên.

### TC-C03 — Đẩy Đông rồi đẩy Bắc: cạnh Bắc lùi đủ

Sau TC-A01 (phòng đã rộng tới `x=3`), player đứng `(1,2)` nhìn Bắc vào `(1,3)`.

**Trước**

```text
y= 3   _ _ _ _ _ _
y= 2   _ . @ . + _
y= 1   _ . . . + _
y= 0   _ . . . + _
y=-1   _ _ _ _ _ _
       -1 0 1 2 3 4
```

**Sau**

```text
y= 4   _ _ _ _ _ _
y= 3   _ + + + + _
y= 2   _ . @ . . _
y= 1   _ . . . . _
y= 0   _ . . . . _
y=-1   _ _ _ _ _ _
       -1 0 1 2 3 4
```

**Pass khi**

- `borderLine = {(0,3),(1,3),(2,3),(3,3)}`: ô `(3,3)` (góc cũ, giờ có sàn `(3,2)` phía sau) cũng thuộc dải.
- Dải dừng ở `(4,3)` vì ô phía trong `(4,2)` là border.
- Góc bù được thêm tại `(4,4)` và `(-1,4)`.
- Phòng thành hình chữ nhật `4x4` và các góc đều kín.
- Khi ợ theo LIFO, map trở lại đúng trạng thái sau TC-A01, rồi về phòng `3x3`.

## 6. Nhóm D — Border nằm trong phòng (`_` vẽ trong map)

### TC-D01 — Vách `_` chạm biên ngoài

```text
y= 2   _ _ _ _ _ _ _
y= 1   _ . @→_ . . _
y= 0   _ . . _ . . _
y=-1   _ _ _ _ _ _ _
       -1 0 1 2 3 4 5
```

Player `(1,1)` nhìn Đông vào `(2,1)`.

**Sau**

```text
y= 2   _ _ _ _ _ _ _
y= 1   _ . @ + _ . _
y= 0   _ . . + _ . _
y=-1   _ _ _ _ _ _ _
       -1 0 1 2 3 4 5
```

**Pass khi**

- `borderLine = {(2,0),(2,1)}`.
- Không có góc bù: `(3,2)` và `(3,-1)` đã là border nên góc đã kín.
- `(2,0)` và `(2,1)` thành sàn; `(3,0)` và `(3,1)` thành border và được ghi vào `emptiedCells`.
- Phòng bên phải hẹp lại 1 cột; hàng biên trên và dưới không đổi.
- Khi ợ, vách về `x=2` và `(3,0)`, `(3,1)` lại là sàn.

### TC-D02 — Vách `_` đứng tự do (không chạm biên)

Vách `_` mà hai đầu đều là sàn.

**Pass khi**

- Dải đẩy được và không có góc bù (hai đầu không phải border).
- Các ô đích loaded được ghi vào `emptiedCells`.
- Player ở phòng bên kia, nếu đứng đúng ô đích, làm lượt đẩy bị hủy (`destination == playerPosition`).

### TC-D03 — Vách trong: box/body phía bên kia

Ô đích là sàn có box hoặc body đứng.

**Pass khi** vật bị đẩy lùi 1 ô cùng vách nếu có chỗ (giống TC-B01/B04); nếu không có chỗ thì cả vách và vật đều đứng yên (giống TC-B02).

## 7. Nhóm E — Cửa

### TC-E01 — Cửa nằm trên dải được đẩy theo

```text
y= 3   _ _ _ _ _
y= 2   _ . . @→_
y= 1   _ . . . D
y= 0   _ . . . _
y=-1   _ _ _ _ _
       -1 0 1 2 3
```

Cửa ở `(3,1)`. Player `(2,2)` nhìn vào ô **bên cạnh** cửa `(3,2)` để không kích hoạt cho cửa ăn.

**Pass khi**

- Cửa chạm dải nên `Shift(E)` một ô và nằm ở `(4,1)`.
- Cửa vẫn mở được từ ô phía trong mới `(3,1)`.
- `movedDoors` chứa cửa này.

### TC-E02 — Nhìn thẳng vào cửa khi cầm food

**Pass khi** cửa phồng (`FeedAndGrow`) và **không** đẩy border. Border chỉ được thử khi `TryFeedDoorAt` trả false.

### TC-E03 — Cửa ẩn (`hiddenUntilRoomComplete`) không ăn được

Player cầm food, nhìn thẳng vào cửa đang ẩn.

**Pass khi**

- Cửa không phồng và border **không** bị đẩy (`IsHiddenDoorAt` chặn trước `TryPushBorder`).
- Phát âm `Nope`; food giữ nguyên; không thêm layer.

### TC-E03b — Cửa ẩn nằm trên dải nhưng player nhìn ô khác

Player nhìn vào một ô border thường, cùng dải với cửa ẩn.

**Pass khi:** cửa ẩn được tính là một phần của tường, nên dải vẫn bị đẩy và cửa ẩn đi theo dải như TC-E01. Cửa vẫn ẩn sau khi dời.

### TC-E04 — Ợ kéo cửa về chỗ cũ

Tiếp từ TC-E01, chờ ợ.

**Pass khi** cửa `Shift(W)` về `(3,1)` trước khi border được phục hồi, và vẫn mở được như ban đầu.

## 8. Nhóm F — Ợ lại

### TC-F01 — Ợ sau 3 giây

Đẩy TC-A01, rồi không làm gì.

**Pass khi**

- Ở khoảng `t ≈ 3s`, map trở về đúng trạng thái trước TC-A01.
- `(3,0..2)` lại là border, `(4,-1..3)` không còn là border.
- Phát âm `Burp`, camera fit lại và stack rỗng.
- Food **không** được hoàn lại.

### TC-F02 — Đẩy liên tiếp reset timer, ợ theo LIFO

1. `t=0`: đẩy Đông (layer 1).
2. `t=2`: đẩy Bắc (layer 2).

**Pass khi**

- Không có gì ợ ở `t=3` vì timer đã reset tại `t=2`.
- `t≈5`: layer 2 (Bắc) ợ trước.
- `t≈8`: layer 1 (Đông) ợ sau.
- Sau mỗi bước, map khớp đúng trạng thái trước lần đẩy tương ứng.

### TC-F03 — Player đứng trên sàn mới bị đẩy vào trong

Sau TC-A01, player đứng `(3,1)`.

**Pass khi**

- Khi đến hạn ợ, player bị `MoveTo((2,1))`, rồi layer được phục hồi trong chu kỳ 0.25s kế tiếp.
- Player không bao giờ bị kẹt trong ô border.

### TC-F04 — Player không có chỗ lùi

Player đứng `(3,1)` và ô `(2,1)` bị chặn (box, body hoặc `#`).

**Pass khi**

- Ợ **chờ**: cứ mỗi 0.25s thử lại và không phục hồi một phần.
- Khi ô `(2,1)` trống, player lùi vào rồi ợ hoàn tất.

**⚠ Ghi chú:** nếu không bao giờ trống, coroutine ở trạng thái chờ mãi. Cần quyết định có muốn timeout hoặc phương án khác không.

### TC-F05 — Box trên sàn mới

Sau TC-A01, đẩy box vào `(3,1)`.

**Pass khi**

- Box bị đẩy `W` sang `(2,1)` rồi ợ hoàn tất.
- Nếu player đứng `(2,1)`, player bị đẩy sang `(1,1)` trước rồi box mới đi.
- Nếu player không lùi được, box đứng yên và ợ chờ.

### TC-F06 — Pet/Oven body chạm sàn mới

Body phồng tràn vào cột `x=3`.

**Pass khi**

- Cả body `TryShift(W)` nguyên khối và không bị biến dạng.
- Nếu body dịch sẽ đè player, player được đẩy trước.
- Nếu body không dịch được, ợ chờ.

### TC-F07 — Ợ trả lại sàn bị đè (`emptiedCells`)

Dùng trường hợp vách trong đẩy được (TC-D02).

**Pass khi** sau khi ợ, các ô `x` lại là sàn đi được và vách trở về chỗ cũ.

### TC-F08 — Ợ không dựng border đè lên thứ gì

**Pass khi** sau mọi lần ợ không có player, box hay body nằm trong ô border, kể cả khi phải chờ nhiều chu kỳ 0.25s.

## 9. Nhóm G — Hiển thị và camera

### TC-G01 — Sprite South Open/Closed

Sau mỗi lần đẩy hoặc ợ, mọi ô border chọn sprite theo ô phía Nam: Closed nếu ô Nam là border, Open nếu không.

**Pass khi** các ô vừa thay đổi và **ô ngay phía Bắc của chúng** cũng được vẽ lại đúng. `GridCellView.Refresh` vẽ lại ô đó và 4 ô kề.

### TC-G02 — Camera fit và pixel-perfect

**Pass khi**

- Sau đẩy và sau ợ, toàn bộ sàn và border nằm trong khung hình.
- Không có đường sọc giữa các tile (zoom snap về số pixel nguyên).

## 10. Checklist assertion sau mỗi lần đẩy hoặc ợ

- Không ô nào vừa là `loadedCells` vừa là `borderCells`.
- Không có player, box hay body nằm trong ô border hoặc void.
- Mọi cạnh trực giao của vùng sàn đều giáp sàn, `#` hoặc border (không lộ void).
- Food chỉ giảm khi `TryPushBorder` trả true; ợ không đổi food.
- `voidPushLayers.Count` tăng 1 sau mỗi lần đẩy thành công và giảm 1 sau mỗi lần ợ.
- Đẩy rồi ợ phải trả về **chính xác** tập sàn, border và vị trí cửa ban đầu.
- Cửa đã dời (`movedDoors`) luôn nằm trên border và vẫn mở được từ ô phía trong.

## 11. Thứ tự chạy test

1. Nhóm A để xác nhận luồng đẩy cơ bản.
2. Nhóm B để xác nhận đẩy thất bại là all-or-nothing.
3. Nhóm F (F01–F03) để xác nhận ợ và hoàn tác chính xác.
4. Nhóm E cho cửa.
5. Nhóm C và D cho các hình dạng đặc biệt; chốt các mục **⚠ Cần quyết định** trước khi sửa code.
6. Nhóm F (F04–F08) cho trường hợp ợ bị chặn.
7. Nhóm G cho hiển thị và camera.
