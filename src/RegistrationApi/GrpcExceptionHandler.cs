using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RegistrationApi;

public class GrpcExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is not RpcException rpc) return false;

        var (status, title) = rpc.StatusCode switch
        {
            StatusCode.NotFound => (404, "Không tìm thấy dữ liệu"),
            StatusCode.Unavailable or StatusCode.DeadlineExceeded
                => (503, "Course Capacity Service không khả dụng"),
            _ => (502, "Lỗi khi gọi Course Capacity Service")
        };

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = rpc.Status.Detail
        }, ct);
        return true;
    }
}
