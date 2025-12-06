using System.ComponentModel.DataAnnotations;

namespace ZISK.Client.Models;

public class LoginModel
{
    [Required(ErrorMessage = "Meno je povinné")]
    [MinLength(3, ErrorMessage = "Meno musí ma? aspo? 3 znaky")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Heslo je povinné")]
    [MinLength(4, ErrorMessage = "Heslo musí ma? aspo? 4 znaky")]
    public string Password { get; set; } = "";
}