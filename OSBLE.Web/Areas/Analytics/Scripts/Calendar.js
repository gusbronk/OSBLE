﻿
var monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
var yearG = 2015, monthG = 01, dayG = 01;
var currentMonth = 1, currentYear = 2014;

$(document).ready(function () {

    $("#back").click(function () { updateCalendar(-1); });
    $("#forward").click(function () { updateCalendar(1); });

    $("input[type='radio']").click(function () {

        updateRadioDependencies();
        updateCalendar(0);
    });

    $("input[type='checkbox']").click(function () {

        if ($("input[type='checkbox']:checked").length < 6) {

            updateMeasureBackground();

            if ($("#hourlychart").is(":visible")) {

                onDayClick(yearG, monthG, dayG, false);
            } else {

                updateCalendar(0);
            }
        } else {

            $(this).prop("checked", false);
            alert("The calendar can only show 5 or less measures at a time!");
        }
    });

    $("#hourly a").click(function () {

        updateCalendar(0);
    });

    updateMeasureBackground();
    updateRadioDependencies();
    updateCalendar(0);
});

function updateCalendar(monthOffset) {

    d3.select("svg").remove();
    updateDisplayArea(false);

    // collect selected measures
    var measures = getSelectedMeasures();
    var dataservice = $("#main").attr("data-service-path");

    currentMonth += monthOffset;
    if (currentMonth > 12)
    {
        currentMonth = 1;
        currentYear++;
    }
    if (currentMonth < 1)
    {
        currentMonth = 12;
        currentYear--;
    }

    if (measures.length > 0) {
        $.ajax({
            url: dataservice + "/api/Calendar/",
            //xhrFields: { withCredentials: true },
            type: "GET",
            headers: { "Access-Control-Allow-Origin": dataservice + ".*" },
            data: {
                ReferenceDate: currentYear + "/" + currentMonth + "/01",
                AggregateFunctionId: $("input[name = 'AggregationFunction']").val(),
                CourseId: $("select[name = 'CourseId']").val(),
                SelectedMeasures: measures
            }
        }).done(function (data) {

            if (data != null) {
                updateDisplayArea(false);

                currentMonth = data.month;
                currentYear = data.year;
                data.month = data.month - 1;
                buildCalendar(data);
            }
            else {
                updateDisplayArea(false);
                showEmptyCalendar()
            }
        });
    }
    else {
        updateDisplayArea(false);
        showEmptyCalendar();
    }
}

function showEmptyCalendar()
{
    var data = { year: currentYear, month: currentMonth - 1, day: 1, measures: [], activities: [] };
    buildCalendar(data);
}

function buildCalendar(data)
{
    $("#currentMonth").text(calendarLabel(data.year, data.month));

    var scalar = $("#chart").width() / 600;

    // calendar
    var chart = d3.trendingCalendar().height(700).onDayClick(onDayClick);
    d3.select("#chart").selectAll("svg").data([data]).enter().append("svg")
        .attr("width", "100%")
        .attr("height", 700*scalar)
        .append("g").attr("transform", "scale(" + scalar + ")")
        .call(chart);

}

function updateMeasureBackground() {

    $("input[type='checkbox']").each(function (i, e) {
        if (!$(e).is(":checked")) {

            $(e).next().css({ "background-color": "transparent", "color": "#333" });
        } else {

            $(e).next().css({ "background-color": $(e).attr("data-color"), "color": "#fff" });
        }
    });
}

function updateRadioDependencies() {

    var aggVal = $("input[type='radio']:checked").val();

    $("input[type='checkbox'][agg-func]").each(function () {
        if ($(this).attr("agg-func") === aggVal) {
            var id = $(this).attr("id");
            $(this).attr("disabled", false).next().attr("for", id);
        }
        else
            $(this).prop("checked", false).attr("disabled", true).next().css({ "background-color": "transparent", "color": "#333" }).attr("for", "");
    });
}

function getSelectedMeasures() {
    var measures = [];
    $("input:checkbox[name='SelectedMeasureTypes']:checked").each(function () {
        measures.push(this.value);
    });
    return measures.toString();
}

function onDayClick(year, month, day) {

    updateDisplayArea(true);

    // month is given as a zero based index. We need it to corespond 
    // to the month number (i.e. Jan = 1, Dec =12)
    month++;

    //the year, month, day are originated from Monthly calendar's day click
    //since user could check or uncheck measures
    //these values need to be preserved globally
    yearG = year, monthG = month, dayG = day;

    d3.select("svg").remove();

    var dataservice = $("#main").attr("data-service-path");
    var measures = getSelectedMeasures();

    if (measures.length > 0)
        $.ajax({
            url: dataservice + "/api/CalendarDay/",
            //xhrFields: { withCredentials: true },
            type: "GET",
            headers: { "Access-Control-Allow-Origin": dataservice + ".*" },
            data: {
                ReferenceDate: year + "/" + month + "/" + day,
                AggregateFunctionId: $("input[name = 'AggregationFunction']").val(),
                CourseId: $("select[name = 'CourseId']").val(),
                SelectedMeasures: measures
            }
        }).done(function (data) {

            //var data = JSON.parse(result);

            if (data != null && data.measures.length > 0) {
                // monthNames is a zero-based array
                $("#currentDay").text(monthNames[month-1] + " " + day + ", " + year);
                drawHourlyChart(data);
            }
            else
            {
                $("#currentDay").text("No Data");
            }

            updateDisplayArea(true);
        });
}

function drawHourlyChart(data) {

    var margin = { top: 20, right: 20, bottom: 30, left: 50 },
        width = 600 - margin.left - margin.right,
        height = 300 - margin.top - margin.bottom;

    var x = d3.scale.linear().domain([0, 24]).range([0, width]);
    var y = d3.scale.linear().domain([0, data.max]).range([height, 0]);

    var xAxis = d3.svg.axis().scale(x).orient("bottom");
    var yAxis = d3.svg.axis().scale(y).orient("left");

    var svg = d3.select("#hourlychart").append("svg")
                .attr("width", width + margin.left + margin.right)
                .attr("height", height + margin.top + margin.bottom)
              .append("g")
                .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

    var line = d3.svg.line()
        .x(function (d) { return x(d.hour); })
        .y(function (d) { return y(d.value); })
        .interpolate("linear");

    svg.append("g")
        .attr("class", "x axis")
        .attr("transform", "translate(0," + height + ")")
        .call(xAxis);

    svg.append("g")
        .attr("class", "y axis")
        .call(yAxis)
        .append("text")
        .attr("transform", "rotate(-90)")
        .attr("y", 6)
        .attr("dy", ".71em")
        .style("text-anchor", "end");

    for (var m = 0; m < data.measures.length; m++) {

        svg.append("path")
            .attr("class", "line")
            .attr("d", line(data.measures[m].values))
            .style("stroke", data.measures[m].color);
    }
}

function updateDisplayArea(hourly) {

    if (hourly) {
        $(".d3-tip:visible").hide();
        $("#hourly").show();
        $("#calendar").hide();
    }
    else {
        $("#hourly").hide();
        $("#calendar").show();
    }
}

function calendarLabel(year, month) {

    var monthToDisplay = new Date(year, month, 1, 0, 0, 0, 0);
    monthToDisplay.setMonth(month + 1);
    var monthTo = monthToDisplay.getMonth();

    if (month < monthTo) {

        return monthNames[month] + " - " + monthNames[monthTo] + " " + year;
    }
    else {

        return monthNames[month] + " " + year + " - " + monthNames[monthTo] + " " + (year + 1);
    }
}
