using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupWizardViewModel : ObservableObject
    {
        private readonly PersonalCloudLibrarySourceSettingsV3 activeSettings;
        private readonly SetupValidationService validationService;
        private SetupWizardStep currentStep = SetupWizardStep.ChooseSource;
        private IList<string> validationErrors = new List<string>();
        private bool isCancelled;
        private bool isCompleted;

        public SetupWizardViewModel(
            PersonalCloudLibrarySourceSettingsV3 activeSettings,
            SetupValidationService validationService)
        {
            this.activeSettings = activeSettings ?? throw new ArgumentNullException(nameof(activeSettings));
            this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            Draft = SetupDraft.FromSettings(activeSettings);
        }

        public SetupDraft Draft { get; }

        public SetupWizardStep CurrentStep
        {
            get => currentStep;
            private set
            {
                SetValue(ref currentStep, value);
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanComplete));
            }
        }

        public IList<string> ValidationErrors
        {
            get => validationErrors;
            private set => SetValue(ref validationErrors, value ?? new List<string>());
        }

        public bool IsCancelled
        {
            get => isCancelled;
            private set => SetValue(ref isCancelled, value);
        }

        public bool IsCompleted
        {
            get => isCompleted;
            private set => SetValue(ref isCompleted, value);
        }

        public bool CanGoBack => !IsCancelled && !IsCompleted && CurrentStep > SetupWizardStep.ChooseSource;
        public bool CanGoNext => !IsCancelled && !IsCompleted && CurrentStep < SetupWizardStep.Review;
        public bool CanComplete => !IsCancelled && !IsCompleted && CurrentStep == SetupWizardStep.Review;

        public void SelectSource(SetupSourceKind sourceKind)
        {
            if (IsCancelled || IsCompleted)
            {
                return;
            }

            Draft.SelectedSource = sourceKind;
            ValidationErrors = new List<string>();
        }

        public bool Next()
        {
            if (!CanGoNext)
            {
                return false;
            }

            var result = validationService.Validate(Draft, CurrentStep);
            ValidationErrors = result.Errors;
            if (!result.IsValid)
            {
                return false;
            }

            CurrentStep = (SetupWizardStep)((int)CurrentStep + 1);
            ValidationErrors = new List<string>();
            return true;
        }

        public bool Back()
        {
            if (!CanGoBack)
            {
                return false;
            }

            CurrentStep = (SetupWizardStep)((int)CurrentStep - 1);
            ValidationErrors = new List<string>();
            return true;
        }

        public bool Complete()
        {
            if (!CanComplete)
            {
                return false;
            }

            var result = validationService.Validate(Draft, SetupWizardStep.Review);
            ValidationErrors = result.Errors;
            if (!result.IsValid)
            {
                return false;
            }

            Draft.ApplyTo(activeSettings);
            IsCompleted = true;
            CurrentStep = SetupWizardStep.Completed;
            ValidationErrors = new List<string>();
            return true;
        }

        public void ReactivateReviewAfterSaveFailure(string message)
        {
            IsCompleted = false;
            CurrentStep = SetupWizardStep.Review;
            ValidationErrors = string.IsNullOrWhiteSpace(message)
                ? new List<string>()
                : new List<string> { message };
        }

        public void Cancel()
        {
            if (IsCompleted)
            {
                return;
            }

            IsCancelled = true;
            ValidationErrors = new List<string>();
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanComplete));
        }
    }
}
