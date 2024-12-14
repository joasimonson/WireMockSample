public class AddressApiSettings
{
    public string BaseUrl { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
}

public class Operation
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string EirCode { get; set; } = null!;
}

public class AddressResponse
{
    public int Id { get; set; }
    public string EirCode { get; set; } = null!;
    public string Street { get; set; } = null!;
}