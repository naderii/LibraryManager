namespace LibraryManager.Models; public class Book { public int Id{get;set;} public string Title{get;set;}=""; public string Author{get;set;}=""; public string Publisher{get;set;}=""; public string ISBN{get;set;}=""; public int PublicationYear{get;set;} public string Category{get;set;}=""; public int TotalCopies{get;set;} public int AvailableCopies{get;set;} public string ShelfLocation{get;set;}=""; public decimal Price{get;set;} public DateTime CreatedAt{get;set;}=DateTime.Now; // Reserved for next version
 public string ImagePath{get;set;}
 public string Description{get;set;} = "";
 public int PageCount{get;set;}
 public string Language{get;set;} = "";
 public string Edition{get;set;} = "";
 public string CoverUrl{get;set;} = ""; }