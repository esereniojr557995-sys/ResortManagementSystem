namespace ResortManagementSystem.Domain.Entities
{
    public class Device
    {
        public int DeviceId { get; set; }
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public int CompanyId { get; set; }

        public Company? Company { get; set; }
    }
}
