namespace MealPlanner.Tests;

public class ShoppingTextTests
{
    [Fact]
    public void Stores_become_bold_headings_with_their_items()
    {
        var text = ShoppingText.Build("Shopping list", [
            new ShareGroup("Lidl", [new ShareLine("Rice", "160 g"), new ShareLine("Coffee", "")]),
            new ShareGroup("Market", []),
            new ShareGroup("Butcher", [new ShareLine(" Minced beef ", "420 g")])
        ]);

        Assert.Equal("Shopping list\n\n*Lidl*\n• Rice: 160 g\n• Coffee\n\n*Butcher*\n• Minced beef: 420 g", text);
    }

    [Fact]
    public void One_store_has_no_heading()
    {
        Assert.Equal("List\n\n• Eggs: 6 pc", ShoppingText.Build("List", [new ShareGroup(null, [new ShareLine("Eggs", "6 pc")])]));
    }
}
