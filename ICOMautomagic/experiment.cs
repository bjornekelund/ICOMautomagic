Application.Current.Dispatcher.Invoke(new Action(() =>
{
	SetupRadio_Edges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[newMHz]);
	BandModeLabel.Content = string.Format(BandModeLabelFormat, bandName[newMHz], newMode);
	RefLevelLabel.Content = string.Format(RefLabelFormat, currentRefLevel);


	// Update displayed information
	LowerEdgeTextbox.Text = currentLowerEdge.ToString();
	UpperEdgeTextbox.Text = currentUpperEdge.ToString();
	RefLevelSlider.Value = currentRefLevel;

	// Update waterfall edges and ref level in radio


}));

Application.Current.Dispatcher.Invoke(new Action(() =>
{
	SetupRadio_Reflevel(currentRefLevel);

	BandModeLabel.Content = string.Format(BandModeLabelFormat, bandName[newMHz], newMode);
	RefLevelLabel.Content = string.Format(RefLabelFormat, currentRefLevel);

	// Update displayed information
	RefLevelSlider.Value = currentRefLevel;

}));
