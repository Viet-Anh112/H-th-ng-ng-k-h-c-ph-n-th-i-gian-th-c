# Hệ Thống Đăng Ký Học Phần Thời Gian Thực (Course Registration System)

Hệ thống phân tán mô phỏng quy trình đăng ký học phần thời gian thực, đảm bảo tính nhất quán dữ liệu, chống vượt sức chứa và xử lý sự kiện bất đồng bộ.

## 🏛️ Kiến Trúc Hệ Thống

1. **CapacityService (gRPC + PostgreSQL)**:
   - Quản lý dữ liệu sức chứa lớp học phần.
   - Sử dụng PostgreSQL với ràng buộc `CHECK ("Registered" >= 0 AND "Registered" <= "Capacity")`.
   - Cập nhật số chỗ bằng câu lệnh SQL nguyên tử (`UPDATE ... WHERE "Registered" < "Capacity"`) để chống tranh chấp đồng thời (race condition).
   - Cung cấp các gRPC endpoint: `GetCourse`, `ListCourses`, `CheckAvailability`, `ReserveSeat`, `ReleaseSeat`.
2. **RegistrationApi (REST API & Web UI)**:
   - Tiếp nhận yêu cầu đăng ký học phần từ người dùng.
   - Gọi sang `CapacityService` qua gRPC để kiểm tra và giữ chỗ.
   - Tích hợp giao diện Web UI trực quan và Swagger UI tại cổng `5000`.
3. **NotificationService (Background / Web)**:
   - Nhận sự kiện đăng ký thành công/hủy qua message broker để gửi thông báo.
4. **AuditWorker (Worker Service)**:
   - Tiêu thụ sự kiện `RegistrationEvent` từ RabbitMQ để ghi log kiểm toán (audit trail), hỗ trợ `EventId` chống xử lý trùng lặp.
5. **Contracts (Shared Library)**:
   - Định nghĩa record sự kiện dùng chung `RegistrationEvent`.

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy

### 1. Yêu cầu môi trường
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (bật WSL2)

### 2. Khởi chạy Database & Message Broker
```bash
docker compose up -d
```
- PostgreSQL: `localhost:5432` (user: `postgres`, pass: `postgres`, db: `capacity_db`)
- RabbitMQ: `localhost:5672` (quản lý tại `http://localhost:15672`, user: `guest`, pass: `guest`)

### 3. Cập nhật Cơ sở dữ liệu (Migration)
```bash
cd src/CapacityService
dotnet ef database update
```

### 4. Khởi chạy các Service
- Chạy **CapacityService**:
  ```bash
  dotnet run --project src/CapacityService
  ```
- Chạy **RegistrationApi**:
  ```bash
  dotnet run --project src/RegistrationApi
  ```

---

## 🧪 Kiểm Thử

- **Giao diện Web UI**: Truy cập [http://localhost:5000](http://localhost:5000) để kiểm thử trực quan và bấm nút *Chạy Tự Động Toàn Bộ 7 Ca Test*.
- **Swagger UI**: Truy cập [http://localhost:5000/swagger](http://localhost:5000/swagger).
- **Postman gRPC**: Kết nối tới `localhost:5101` hoặc `localhost:5100` (bật *Use server reflection*).
