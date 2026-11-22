namespace CapacityService.Data;

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Capacity { get; set; }      // sức chứa tối đa
    public int Registered { get; set; }    // số đã đăng ký
}
