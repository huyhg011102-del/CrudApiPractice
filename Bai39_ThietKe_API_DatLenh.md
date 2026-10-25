# Bài 39. Thiết kế API đặt lệnh mua chứng khoán ⭐

> Đề: tạo lệnh, hủy lệnh, xem trạng thái, lịch sử khớp. Nêu rõ endpoint/method, request/response, các trạng thái, xử lý đồng thời khi nhiều lệnh cùng khớp, và idempotency khi client gửi trùng.
>
> Khi trả lời ở buổi phỏng vấn, đi theo thứ tự: **làm rõ yêu cầu → tài nguyên & endpoint → model dữ liệu → vòng đời trạng thái → đồng thời → idempotency → lỗi & bảo mật**. Mất khoảng 10–15 phút nói, vẽ thêm sơ đồ trạng thái lên bảng.

---

## 0. Hỏi lại trước khi thiết kế (ghi điểm)

- Chỉ lệnh **LO** (giới hạn giá), hay có cả **ATO/ATC/MP**?
- Có **khớp một phần** không? (Có, theo lô 100 cổ phiếu.)
- Ai khớp lệnh: hệ thống mình tự khớp, hay **gửi lên Sở (HOSE/HNX)** rồi nhận kết quả về? Thực tế công ty chứng khoán là trường hợp thứ hai: API của mình nhận lệnh, kiểm tra, **phong tỏa tiền**, đẩy lên Sở, nhận lại các lần khớp.
- Biên độ giá, bước giá, giờ giao dịch theo từng sàn.
- Lệnh có hiệu lực trong ngày (DAY) hay tới khi hủy (GTC)?

Phần dưới giả định: lệnh LO, có khớp một phần, hiệu lực trong ngày, khớp ở hệ thống bên ngoài (Sở) và trả về qua message queue.

---

## 1. Endpoint

Tài nguyên chính là `orders`, tài nguyên con là `fills` (các lần khớp).

| Method | Endpoint | Mục đích | Thành công |
|---|---|---|---|
| `POST` | `/api/v1/orders` | Tạo lệnh. **Bắt buộc** header `Idempotency-Key` | `201 Created` + `Location` |
| `GET` | `/api/v1/orders/{orderId}` | Xem trạng thái một lệnh | `200` |
| `GET` | `/api/v1/orders?status=&symbol=&from=&to=&cursor=&limit=` | Sổ lệnh của tài khoản (phân trang cursor) | `200` |
| `POST` | `/api/v1/orders/{orderId}/cancel` | **Yêu cầu** hủy lệnh | `202 Accepted` |
| `GET` | `/api/v1/orders/{orderId}/fills` | Lịch sử khớp của một lệnh | `200` |
| `GET` | `/api/v1/fills?from=&to=&symbol=` | Lịch sử khớp của tài khoản | `200` |

**Vì sao hủy dùng `POST .../cancel` mà không dùng `DELETE /orders/{id}`?**
Hủy là một **lệnh nghiệp vụ gửi lên Sở**, không phải xóa bản ghi. Lệnh vẫn tồn tại (chuyển sang `PendingCancel`, rồi `Cancelled`), và có thể **không hủy được** nếu đã khớp hết trước đó. Vì kết quả là bất đồng bộ nên trả `202 Accepted` thay vì `204`.

`accountId` lấy từ **JWT**, không nhận từ body. Như vậy user không đặt lệnh hộ tài khoản của người khác được.

---

## 2. Request / Response

### Tạo lệnh

```http
POST /api/v1/orders
Authorization: Bearer <jwt>
Idempotency-Key: 7f1c2e9a-3b4d-4c55-9a10-2b8f6d1e0c33
Content-Type: application/json

{
  "symbol": "FPT",
  "side": "Buy",
  "orderType": "LO",
  "quantity": 1000,
  "price": 125500,
  "timeInForce": "Day"
}
```

```http
HTTP/1.1 201 Created
Location: /api/v1/orders/ORD-20261025-000123

{
  "orderId": "ORD-20261025-000123",
  "symbol": "FPT",
  "side": "Buy",
  "orderType": "LO",
  "quantity": 1000,
  "price": 125500,
  "filledQuantity": 0,
  "remainingQuantity": 1000,
  "avgFilledPrice": null,
  "status": "PendingNew",
  "blockedAmount": 125625500,
  "createdAt": "2026-10-25T02:15:03.120Z",
  "version": 1
}
```

`blockedAmount` = giá × khối lượng + phí ước tính, là số tiền bị **phong tỏa** ngay khi đặt lệnh.

### Validation (400 / 422)

| Kiểm tra | Mã lỗi |
|---|---|
| Thiếu field, sai kiểu, `quantity <= 0` | `400` |
| `quantity` không chia hết cho lô 100 | `422` `INVALID_LOT_SIZE` |
| Giá ngoài biên độ trần/sàn, sai bước giá | `422` `PRICE_OUT_OF_RANGE` |
| Ngoài giờ giao dịch | `422` `MARKET_CLOSED` |
| Sức mua không đủ | `422` `INSUFFICIENT_BUYING_POWER` |
| Trùng `Idempotency-Key` nhưng **body khác** | `422` `IDEMPOTENCY_KEY_REUSED` |
| Request trùng key **đang xử lý** | `409` `REQUEST_IN_PROGRESS` |

Lỗi trả về theo ProblemDetails, kèm `code` để client xử lý theo máy:

```json
{ "type": "https://api.example.com/errors/insufficient-buying-power",
  "title": "Sức mua không đủ", "status": 422, "code": "INSUFFICIENT_BUYING_POWER",
  "detail": "Cần 125.625.500đ, khả dụng 80.000.000đ", "traceId": "..." }
```

### Lịch sử khớp

```json
GET /api/v1/orders/ORD-20261025-000123/fills
[
  { "fillId": "F-889102", "quantity": 300, "price": 125500, "matchedAt": "2026-10-25T02:15:04.010Z" },
  { "fillId": "F-889140", "quantity": 200, "price": 125400, "matchedAt": "2026-10-25T02:16:11.552Z" }
]
```

---

## 3. Trạng thái lệnh (state machine)

```
                ┌──────────► Rejected            (Sở hoặc kiểm tra rủi ro từ chối)
                │
 PendingNew ──► New ──► PartiallyFilled ──► Filled           (khớp hết)
                 │            │
                 │            ├──► PendingCancel ──► Cancelled   (hủy phần còn lại)
                 ├──► PendingCancel ──► Cancelled
                 │
                 └──► Expired                    (hết phiên, lệnh Day chưa khớp hết)
```

| Trạng thái | Ý nghĩa | Hủy được? |
|---|---|---|
| `PendingNew` | Đã nhận, đã phong tỏa tiền, đang gửi lên Sở | Có |
| `New` | Sở đã chấp nhận, nằm trên sổ lệnh | Có |
| `PartiallyFilled` | Đã khớp một phần | Có (hủy phần còn lại) |
| `PendingCancel` | Đã gửi yêu cầu hủy, chờ Sở xác nhận | Không |
| `Filled` | Khớp hết (**trạng thái cuối**) | Không → `409` |
| `Cancelled` / `Rejected` / `Expired` | **Trạng thái cuối**. Giải tỏa số tiền còn phong tỏa | Không → `409` |

Chuyển trạng thái chỉ được đi **theo các mũi tên**, và kiểm tra trong code (ví dụ `order.CanTransitionTo(newStatus)`). Ví dụ: một lần khớp tới khi lệnh đang `PendingCancel` vẫn **phải được ghi nhận**, vì Sở khớp trước khi kịp hủy. Khi đó lệnh có thể kết thúc là `Filled` dù user đã bấm hủy.

---

## 4. Model dữ liệu

```
Orders (OrderId PK, AccountId, Symbol, Side, OrderType, Price, Quantity,
        FilledQuantity, AvgFilledPrice, Status, BlockedAmount,
        IdempotencyKey, CreatedAt, UpdatedAt, RowVersion)
        UNIQUE (AccountId, IdempotencyKey)

Fills  (FillId PK,            ← ExecutionId do Sở cấp: UNIQUE → chống xử lý trùng
        OrderId FK, Quantity, Price, MatchedAt)

Accounts (AccountId PK, CashBalance, BlockedCash, RowVersion)

IdempotencyRecords (AccountId, Key, RequestHash, StatusCode, ResponseBody, CreatedAt, ExpiresAt)
        PK (AccountId, Key)
```

Bất biến cần giữ: `FilledQuantity = SUM(Fills.Quantity)` và `FilledQuantity <= Quantity`.

---

## 5. Xử lý đồng thời khi nhiều lệnh cùng khớp ⭐

Có ba chỗ dễ xảy ra race condition:

### a) Nhiều lần khớp của **cùng một lệnh** tới gần như đồng thời

Ví dụ hai message khớp 300 và 200 cổ phiếu được xử lý song song. Nếu cả hai cùng đọc `FilledQuantity = 0` rồi ghi đè, ta mất 300 cổ phiếu.

- **Cách 1, cập nhật nguyên tử bằng SQL**, không làm read-modify-write trong C#:
  ```sql
  UPDATE Orders
  SET FilledQuantity = FilledQuantity + @qty,
      Status = CASE WHEN FilledQuantity + @qty = Quantity THEN 'Filled' ELSE 'PartiallyFilled' END
  WHERE OrderId = @id AND FilledQuantity + @qty <= Quantity;     -- không vượt quá khối lượng đặt
  ```
- **Cách 2, optimistic concurrency** (`RowVersion`, giống bài 24): bắt `DbUpdateConcurrencyException`, đọc lại rồi thử lại.
- **Cách 3 (hay dùng nhất ở hệ thống giao dịch), xử lý tuần tự theo key:** message khớp được partition theo `OrderId` (Kafka partition key, hoặc queue có session/group id). Cùng một lệnh luôn được **một consumer xử lý tuần tự**, khác lệnh thì chạy song song. Cách này tránh tranh chấp ngay từ thiết kế.

### b) **Cùng một sự kiện khớp được gửi lại** (message queue thường đảm bảo "at-least-once")

Dùng `FillId` = ExecutionId của Sở làm **khóa chính hoặc unique**. Lần gửi thứ hai vi phạm unique, ta bỏ qua. Tức là **consumer cũng phải idempotent**. Ghi bản ghi Fill và cập nhật Order trong **cùng một transaction**.

### c) **Sức mua**: hai lệnh mua đặt cùng lúc, cùng dùng một khoản tiền

```sql
UPDATE Accounts
SET BlockedCash = BlockedCash + @amount
WHERE AccountId = @acc AND CashBalance - BlockedCash >= @amount;   -- kiểm tra và trừ trong MỘT câu
-- @@ROWCOUNT = 0 → không đủ sức mua → 422
```

Không làm kiểu "SELECT số dư → if đủ → UPDATE" vì hai request sẽ cùng thấy "đủ".

### d) **Hủy và khớp xảy ra cùng lúc**

Hủy chỉ đặt trạng thái `PendingCancel` và gửi lên Sở. Sở mới là nơi phân xử: khớp trước thì `Filled`, hủy trước thì `Cancelled`. Phía mình chỉ cập nhật theo sự kiện Sở trả về, và kiểm tra state machine.

---

## 6. Idempotency khi client gửi trùng request ⭐

**Tình huống:** client bấm "Đặt lệnh", mạng timeout, client **gửi lại**. Nếu không xử lý, hệ thống sẽ đặt **hai lệnh** và phong tỏa tiền hai lần.

**Giải pháp:** header `Idempotency-Key` (UUID do **client** sinh, giữ nguyên khi retry).

```
1. Nhận request (AccountId, Key, hash(body))
2. INSERT IdempotencyRecords (AccountId, Key, RequestHash, Status = 'Processing')
     ├─ thành công → lần đầu: xử lý tạo lệnh,
     │                 lưu StatusCode + ResponseBody vào record, trả về client
     └─ vi phạm PK (key đã có):
          ├─ RequestHash khác       → 422 IDEMPOTENCY_KEY_REUSED (dùng lại key cho request khác)
          ├─ Status = 'Processing'  → 409 REQUEST_IN_PROGRESS (lần đầu chưa xong, client đợi rồi thử lại)
          └─ đã xong                → trả lại NGUYÊN response đã lưu (201 + cùng orderId)
3. Record hết hạn sau 24h (job dọn dẹp)
```

Điểm mấu chốt:
- **Dùng unique constraint của DB** để chống race. Hai request trùng tới cùng lúc thì chỉ một cái INSERT thành công. Không dùng kiểu "kiểm tra rồi mới ghi" trong code.
- Lưu và trả lại **đúng response cũ**, nhờ đó client không phân biệt được lần đầu hay lần gửi lại.
- Thêm một lớp bảo vệ: `UNIQUE (AccountId, IdempotencyKey)` ngay trên bảng `Orders`.
- Tính chất idempotent của các method: `GET` vốn idempotent. `POST /cancel` cũng nên idempotent, tức là hủy một lệnh đang `PendingCancel` hoặc `Cancelled` thì trả `202` hoặc `200` với trạng thái hiện tại, không báo lỗi.

---

## 7. Những điểm khác nên nhắc nhanh

- **Bảo mật:** JWT, user chỉ xem và thao tác lệnh của `accountId` mình. Truy cập lệnh của người khác thì trả **404**, không trả 403, để khỏi lộ sự tồn tại của lệnh. Có thể cần xác thực 2 bước (OTP/Smart OTP) khi đặt lệnh.
- **Rate limit** theo tài khoản (bài 32), chống bot spam lệnh.
- **Audit log:** mọi thay đổi trạng thái đều được ghi lại (ai, lúc nào, từ trạng thái gì sang gì). Đây là yêu cầu bắt buộc trong ngành chứng khoán.
- **Real-time:** client nhận cập nhật trạng thái và khớp lệnh qua **WebSocket/SignalR**, không phải poll liên tục.
- **Outbox pattern:** ghi lệnh vào DB và "sự kiện cần gửi lên Sở" vào bảng Outbox trong **cùng transaction**, sau đó một worker gửi đi. Nhờ vậy không bao giờ xảy ra chuyện lưu DB xong nhưng message chưa gửi (hoặc ngược lại).
- **Giờ hệ thống:** lưu UTC, hiển thị theo giờ Việt Nam. Phiên giao dịch thì kiểm tra theo giờ Việt Nam.
- **Tiền:** dùng `decimal`, không dùng `double`.
