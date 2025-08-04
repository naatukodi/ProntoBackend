namespace Valuation.Api.Models;
public class StateModel
{
    public string Key { get; set; }   // maps from RowKey
    public string Name { get; set; }   // maps from StateName
    public int DistrictCount { get; set; }
}
