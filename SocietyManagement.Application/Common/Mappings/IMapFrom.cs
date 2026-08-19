using AutoMapper;

namespace SocietyManagement.Application.Common.Mappings;

/// <summary>DTOs implement IMapFrom&lt;Entity&gt; and override Mapping only when the
/// default (property-name-matching) map isn't enough. MappingProfile scans every
/// type implementing this in the assembly, so adding a DTO is enough — no manual
/// CreateMap registration required.</summary>
public interface IMapFrom<T>
{
    void Mapping(Profile profile) => profile.CreateMap(typeof(T), GetType());
}
