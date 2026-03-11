namespace Creaturedex.Core.Entities;

public class ReferenceColour
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? HexValue { get; set; }
    public int SortOrder { get; set; }
}

public class ReferenceTag
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string TagGroup { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ReferenceHabitatType
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ReferenceDietType
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ReferenceActivityPattern
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ReferenceConservationStatus
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Severity { get; set; }
    public string Colour { get; set; } = "";
}

public class ReferenceDomesticationStatus
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsPet { get; set; }
    public int SortOrder { get; set; }
}
