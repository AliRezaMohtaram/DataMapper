using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Web.ViewModels;

namespace Mapper.Tests;

public class DateFormattingTests
{
    [Theory]
    [InlineData("5/27/2015", "۱۳۹۴/۰۳/۰۶")]          // month first, as imported files write it
    [InlineData("27/5/2015", "۱۳۹۴/۰۳/۰۶")]          // day first when the first number cannot be a month
    [InlineData("2015-10-06", "۱۳۹۴/۰۷/۱۴")]
    [InlineData("2015/10/06 14:30", "۱۳۹۴/۰۷/۱۴ ۱۴:۳۰")]
    [InlineData("10/6/2015 2:05 PM", "۱۳۹۴/۰۷/۱۴ ۱۴:۰۵")]
    [InlineData("1405/07/22", "1405/07/22")]         // already Jalali: left alone
    [InlineData("2/30/2015", "2/30/2015")]           // not a date
    [InlineData("I-3337", "I-3337")]
    public void Text_that_is_a_Gregorian_date_shows_in_Jalali(string stored, string shown)
    {
        Assert.Equal(shown, stored.ToRecordDisplay(FieldDataType.Text));
    }
}
